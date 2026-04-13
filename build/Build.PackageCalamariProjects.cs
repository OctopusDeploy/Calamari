using System.Collections.Concurrent;
using System.Collections.Generic;
using Nuke.Common.ProjectModel;

namespace Calamari.Build;

public partial class Build
{
    List<CalamariPackageMetadata> PackagesToPublish = new();
    List<Project> CalamariProjects = new();

    Target GetCalamariFlavourProjectsToPublish =>
        d =>
            d.DependsOn(RestoreSolution)
             .Executes(() =>
                       {
                           var projectNames = GetCalamariFlavours();

                           // Calamari.Scripting is a library that other calamari flavours depend on; not a flavour on its own right.
                           // Unlike other *Calamari* tests, we would still want to produce Calamari.Scripting.Zip and its tests, like its flavours.
                           //We put this at the front of the list so they build in the correct order?
                           projectNames = [..projectNames, "Calamari.Scripting"];

                           //its assumed each project has a corresponding test project
                           var testProjectNames = projectNames.Select(f => $"{f}.Tests");
                           var allProjectNames = projectNames.Concat(testProjectNames).ToHashSet();

                           var calamariProjects = Solution.Projects
                                                          .Where(project => allProjectNames.Contains(project.Name))
                                                          .ToList();

                           CalamariProjects = calamariProjects;

                           //all packages are cross-platform
                           var packages = calamariProjects
                                          .SelectMany(project => GetRuntimeIdentifiers(project)
                                                          .Select(rid =>
                                                                  {
                                                                      //we are making the bold assumption all projects only have a single target framework
                                                                      var framework = project.GetTargetFrameworks()?.Single() ?? Frameworks.Net80;
                                                                      return new CalamariPackageMetadata(project, framework, rid);
                                                                  }))
                                          .ToList();

                           PackagesToPublish = packages;

                           Log.Information("Packages to publish:");
                           foreach (var calamariPackageMetadata in packages)
                           {
                               Log.Information("Project: {Project}, Framework: {Framework}, Arch: {Architecture}", calamariPackageMetadata.Project.Name, calamariPackageMetadata.Framework, calamariPackageMetadata.Architecture);
                           }
                       });

    Target BuildCalamariProjects =>
        d =>
            d.DependsOn(GetCalamariFlavourProjectsToPublish)
             .DependsOn(PublishAzureWebAppNetCoreShim)
             .Executes(async () =>
                       {
                           // First, build the entire solution once without a RID. This compiles all
                           // shared libraries and flavour projects for their default target framework.
                           // MSBuild handles dependency ordering and parallelism internally.
                           DotNetBuild(s =>
                                           s.SetProjectFile(Solution)
                                            .SetConfiguration(Configuration)
                                            .SetVersion(NugetVersion.Value)
                                            .SetInformationalVersion(OctoVersionInfo.Value?.InformationalVersion)
                                            .SetVerbosity(BuildVerbosity));

                           // Then build per-RID, grouped by RID: sequential within each RID to avoid
                           // file contention on shared dependency bin/ directories, parallel across RIDs.
                           // The solution build above ensures shared libraries are already compiled,
                           // so these builds only do RID-specific work for each flavour project.
                           var buildsByRid = PackagesToPublish
                                             .GroupBy(p => p.Architecture)
                                             .ToList();

                           var ridTasks = buildsByRid.Select(async ridGroup =>
                                                             {
                                                                 foreach (var calamariPackageMetadata in ridGroup)
                                                                 {
                                                                     Log.Information("Building {ProjectName} for framework '{Framework}' and arch '{Architecture}'",
                                                                                     calamariPackageMetadata.Project.Name, calamariPackageMetadata.Framework, calamariPackageMetadata.Architecture);

                                                                     await Task.Run(() => DotNetBuild(s =>
                                                                                                          s.SetProjectFile(calamariPackageMetadata.Project)
                                                                                                           .SetConfiguration(Configuration)
                                                                                                           .SetFramework(calamariPackageMetadata.Framework)
                                                                                                           .SetRuntime(calamariPackageMetadata.Architecture)
                                                                                                           .SetVersion(NugetVersion.Value)
                                                                                                           .SetInformationalVersion(OctoVersionInfo.Value?.InformationalVersion)
                                                                                                           .SetVerbosity(BuildVerbosity)
                                                                                                           .EnableSelfContained()));
                                                                 }
                                                             });

                           await Task.WhenAll(ridTasks);
                       });

    Target PublishCalamariProjects =>
        d =>
            d.DependsOn(BuildCalamariProjects)
             .Executes(async () =>
                       {
                           var outputPaths = new ConcurrentBag<AbsolutePath?>();

                           // Publish grouped by RID: sequential within each RID to avoid file
                           // contention on shared dependency bin/ directories, parallel across RIDs.
                           Log.Information("Publishing projects...");
                           var publishByRid = PackagesToPublish
                                              .GroupBy(p => p.Architecture)
                                              .ToList();

                           var ridTasks = publishByRid.Select(async ridGroup =>
                                                              {
                                                                  foreach (var package in ridGroup)
                                                                  {
                                                                      var outputPath = await PublishPackageAsync(package);
                                                                      outputPaths.Add(outputPath);
                                                                  }
                                                              });

                           await Task.WhenAll(ridTasks);

                           // Sign and compress tasks
                           Log.Information("Signing published binaries...");
                           var signTasks = outputPaths
                                           .Where(output => output != null && !output.ToString().Contains("Tests"))
                                           .Select(output => Task.Run(() => SignDirectory(output!)))
                                           .ToList();

                           await Task.WhenAll(signTasks);

                           Log.Information("Compressing published projects...");
                           var compressTasks = CalamariProjects.Select(async proj => await CompressCalamariProject(proj));
                           await Task.WhenAll(compressTasks);
                       });

    async Task<AbsolutePath?> PublishPackageAsync(CalamariPackageMetadata calamariPackageMetadata)
    {
        Log.Information("Publishing {ProjectName} for framework '{Framework}' and arch '{Architecture}'", calamariPackageMetadata.Project.Name, calamariPackageMetadata.Framework, calamariPackageMetadata.Architecture);

        var project = calamariPackageMetadata.Project;
        var outputDirectory = KnownPaths.PublishDirectory / project.Name / calamariPackageMetadata.Architecture;

        await Task.Run(() =>
                           DotNetPublish(s =>
                                             s.SetConfiguration(Configuration)
                                              .SetProject(project)
                                              .SetFramework(calamariPackageMetadata.Framework)
                                              .SetRuntime(calamariPackageMetadata.Architecture)
                                              .SetVersion(NugetVersion.Value)
                                              .SetInformationalVersion(OctoVersionInfo.Value?.InformationalVersion)
                                              .SetVerbosity(BuildVerbosity)
                                              .EnableNoBuild()
                                              .EnableNoRestore()
                                              .EnableSelfContained()
                                              .SetOutput(outputDirectory)));

        File.Copy(KnownPaths.RootDirectory / "global.json", outputDirectory / "global.json");

        return outputDirectory;
    }

    async Task CompressCalamariProject(Project project)
    {
        Log.Information($"Compressing Calamari flavour {KnownPaths.PublishDirectory}/{project.Name}");
        var compressionSource = KnownPaths.PublishDirectory / project.Name;
        if (!Directory.Exists(compressionSource))
        {
            Log.Information($"Skipping compression for {project.Name} since nothing was built");
            return;
        }

        await Task.Run(() => compressionSource.CompressTo($"{KnownPaths.ArtifactsDirectory / project.Name}.zip"));
    }
}
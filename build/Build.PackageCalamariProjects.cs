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
             .Executes(() =>
                       {
                           // Build the solution once without a RID. Per-RID self-contained
                           // compilation happens in the publish step.
                           DotNetBuild(s =>
                                           s.SetProjectFile(Solution)
                                            .SetConfiguration(Configuration)
                                            .SetVersion(NugetVersion.Value)
                                            .SetInformationalVersion(OctoVersionInfo.Value?.InformationalVersion)
                                            .SetVerbosity(BuildVerbosity)
                                            .EnableNoRestore());
                       });

    Target PublishCalamariProjects =>
        d =>
            d.DependsOn(BuildCalamariProjects)
             .Executes(async () =>
                       {
                           var outputPaths = new ConcurrentBag<AbsolutePath?>();

                           // Parallel across RIDs, sequential within each RID.
                           // --no-restore prevents project.assets.json contention.
                           // Sequential within RID avoids Unzip post-build target contention.
                           Log.Information("Publishing projects...");
                           var ridTasks = PackagesToPublish
                                          .GroupBy(p => p.Architecture)
                                          .Select(async ridGroup =>
                                                  {
                                                      foreach (var package in ridGroup)
                                                      {
                                                          var outputPath = await PublishPackageAsync(package);
                                                          outputPaths.Add(outputPath);
                                                      }
                                                  });

                           await Task.WhenAll(ridTasks);

                           // Overlay the standalone Docker credential helper into each Calamari runtime folder
                           // so Docker can invoke `docker-credential-octopus` directly from the deployed package.
                           var calamariProject = Solution.AllProjects.FirstOrDefault(p => p.Name == "Calamari")
                                                 ?? throw new InvalidOperationException("Could not find the 'Calamari' project to overlay the Docker credential helper into.");
                           var helperProject = Solution.AllProjects.FirstOrDefault(p => p.Name == "Calamari.DockerCredentialHelper")
                                               ?? throw new InvalidOperationException("Could not find the 'Calamari.DockerCredentialHelper' project.");
                           foreach (var rid in GetRuntimeIdentifiers(calamariProject))
                           {
                               var calamariRidDirectory = KnownPaths.PublishDirectory / "Calamari" / rid;
                               var helperRidDirectory = KnownPaths.PublishDirectory / "Calamari.DockerCredentialHelper" / rid;

                               // Must be self-contained to match PublishPackageAsync's EnableSelfContained:
                               // Calamari ships its own runtime, and a framework-dependent helper apphost would
                               // not use those loose runtime files, so it would fail to start on targets without
                               // a registered .NET runtime. Publish to a separate folder first, then overlay it
                               // into Calamari's folder, verifying that any shared files are byte-identical.
                               DotNetPublish(s => s
                                                  .SetConfiguration(Configuration)
                                                  .SetProject(helperProject)
                                                  .SetFramework(Frameworks.Net80)
                                                  .SetRuntime(rid)
                                                  .EnableSelfContained()
                                                  .SetOutput(helperRidDirectory));

                               OverlayHelperIntoCalamari(helperRidDirectory, calamariRidDirectory, rid);
                               helperRidDirectory.DeleteDirectory();
                           }

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

    // Copies the helper's published files into Calamari's runtime folder. Files Calamari already
    // ships (the shared runtime, Calamari.Common, etc.) must match by version — if a shared
    // dependency has diverged between the two projects we fail loudly rather than silently
    // overwriting Calamari's copy. Files Calamari already has are left untouched.
    static void OverlayHelperIntoCalamari(AbsolutePath helperDirectory, AbsolutePath calamariDirectory, string rid)
    {
        var source = helperDirectory.ToString();
        var destination = calamariDirectory.ToString();

        foreach (var sourceFile in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(source, sourceFile);
            var destinationFile = Path.Combine(destination, relativePath);

            if (File.Exists(destinationFile))
            {
                if (!FilesAreEquivalent(sourceFile, destinationFile))
                    throw new Exception($"Cannot overlay docker-credential-octopus for {rid}: '{relativePath}' differs in version from Calamari's copy. " +
                                        "A shared dependency has diverged between Calamari and Calamari.DockerCredentialHelper.");
                continue;
            }

            var destinationFileDirectory = Path.GetDirectoryName(destinationFile);
            if (destinationFileDirectory != null)
                Directory.CreateDirectory(destinationFileDirectory);

            Log.Information("Adding {RelativePath} to Calamari/{Rid}", relativePath, rid);
            File.Copy(sourceFile, destinationFile);
        }
    }

    // Shared dependencies must match by file version. We deliberately don't compare raw bytes:
    // two independent (deterministic) builds of the same managed assembly still differ in their
    // module version id (MVID), so only a version mismatch indicates a genuinely divergent dependency.
    static bool FilesAreEquivalent(string first, string second)
    {
        var firstVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(first).FileVersion;
        var secondVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(second).FileVersion;

        if (!string.IsNullOrEmpty(firstVersion) || !string.IsNullOrEmpty(secondVersion))
            return firstVersion == secondVersion;

        // No version metadata (e.g. data files) — fall back to a size comparison.
        return new FileInfo(first).Length == new FileInfo(second).Length;
    }
}
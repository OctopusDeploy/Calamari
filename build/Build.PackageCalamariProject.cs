using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Nuke.Common.ProjectModel;
using Octopus.Calamari.ConsolidatedPackage;

namespace Calamari.Build;

public partial class Build
{
    // By default, all projects are built unless the parameter is set specifically
    // Although all libraries/flavours now support .NET Core, ServiceFabric can currently only be built on Windows devices
    // This is here purely to make the local build experience on non-Windows devices (with testing) workable
    [Parameter(Name = "ProjectToBuild")] readonly string[] ProjectsToBuild = BuildableCalamariProjects.GetCalamariProjectsToBuild(OperatingSystem.IsWindows());
    
    

    List<CalamariPackageMetadata> PackagesToPublish = new();
    List<Project> CalamariProjects = new();

    Target GetCalamariFlavourProjectsToPublish =>
        d =>
            d.DependsOn(RestoreSolution)
             .Executes(() =>
                       {
                           // Calamari.Scripting is a library that other calamari flavours depend on; not a flavour on its own right.
                           // Unlike other *Calamari* tests, we would still want to produce Calamari.Scripting.Zip and its tests, like its flavours.
                           string[] projectNames = [..ProjectsToBuild, "Calamari.Scripting"];

                           //its assumed each project has a corresponding test project
                           var testProjectNames = projectNames.Select(p => $"{p}.Tests");
                           
                           var allProjectNames = projectNames.Concat(testProjectNames).ToHashSet();

                           var calamariProjects = Solution.Projects.Where(project => allProjectNames.Contains(project.Name)).ToList();

                           CalamariProjects = calamariProjects;

                           //all packages are cross-platform
                           var packages = calamariProjects
                                          .SelectMany(project => GetRuntimeIdentifiers(project)
                                                          .Select(rid =>
                                                                  {
                                                                      //we are making the bold assumption all projects only have a single target framework and that if they don't, it's .NET 8.0
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
             .Executes(async () =>
                       {
                           var globalSemaphore = new SemaphoreSlim(1);
                           var semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

                           var buildTasks = PackagesToPublish.Select(async calamariPackageMetadata =>
                                                                     {
                                                                         var projectName = calamariPackageMetadata.Project.Name;
                                                                         var projectSemaphore = semaphores.GetOrAdd(projectName, _ => new SemaphoreSlim(1, 1));
                                                                         var architectureSemaphore = semaphores.GetOrAdd(calamariPackageMetadata.Architecture, _ => new SemaphoreSlim(1, 1));

                                                                         await globalSemaphore.WaitAsync();
                                                                         await projectSemaphore.WaitAsync();
                                                                         await architectureSemaphore.WaitAsync();
                                                                         try
                                                                         {
                                                                             Log.Information($"Building {calamariPackageMetadata.Project.Name} for framework '{calamariPackageMetadata.Framework}' and arch '{calamariPackageMetadata.Architecture}'");

                                                                             await Task.Run(() => DotNetBuild(s =>
                                                                                                                  s.SetProjectFile(calamariPackageMetadata.Project)
                                                                                                                   .SetConfiguration(Configuration)
                                                                                                                   .SetFramework(calamariPackageMetadata.Framework)
                                                                                                                   .SetRuntime(calamariPackageMetadata.Architecture)
                                                                                                                   .EnableSelfContained()));
                                                                         }
                                                                         finally
                                                                         {
                                                                             projectSemaphore.Release();
                                                                             architectureSemaphore.Release();
                                                                             globalSemaphore.Release();
                                                                         }
                                                                     });

                           await Task.WhenAll(buildTasks);
                       });

    Target PublishCalamariProjects =>
        d =>
            d.DependsOn(BuildCalamariProjects)
             .Executes(async () =>
                       {
                           var semaphore = new SemaphoreSlim(4);
                           var outputPaths = new ConcurrentBag<AbsolutePath?>();

                           Log.Information("Publishing projects...");
                           var publishTasks = PackagesToPublish.Select(async package =>
                                                                       {
                                                                           await semaphore.WaitAsync();
                                                                           try
                                                                           {
                                                                               var outputPath = await PublishPackageAsync(package);
                                                                               outputPaths.Add(outputPath);
                                                                           }
                                                                           finally
                                                                           {
                                                                               semaphore.Release();
                                                                           }
                                                                       });

                           await Task.WhenAll(publishTasks);

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
        var outputDirectory = PublishDirectory / project.Name / calamariPackageMetadata.Architecture;

        await Task.Run(() =>
                           DotNetPublish(s =>
                                             s.SetConfiguration(Configuration)
                                              .SetProject(project)
                                              .SetFramework(calamariPackageMetadata.Framework)
                                              .SetRuntime(calamariPackageMetadata.Architecture)
                                              .EnableNoBuild()
                                              .EnableNoRestore()
                                              .SetSelfContained(OperatingSystem.IsWindows() || !IsLocalBuild) // This is here purely to make the local build experience on non-Windows devices workable - Publish breaks on non-Windows platforms with SelfContained = true
                                              .SetOutput(outputDirectory)));

        File.Copy(RootDirectory / "global.json", outputDirectory / "global.json");

        return outputDirectory;
    }

    async Task CompressCalamariProject(Project project)
    {
        Log.Information($"Compressing Calamari flavour {PublishDirectory}/{project.Name}");
        var compressionSource = PublishDirectory / project.Name;
        if (!Directory.Exists(compressionSource))
        {
            Log.Information($"Skipping compression for {project.Name} since nothing was built");
            return;
        }

        await Task.Run(() => compressionSource.CompressTo($"{ArtifactsDirectory / project.Name}.zip"));
    }

    IReadOnlyCollection<string> GetRuntimeIdentifiers(Project? project)
    {
        if (project is null)
            return [];

        var runtimes = project.GetRuntimeIdentifiers();

        if (!string.IsNullOrWhiteSpace(TargetRuntime))
            runtimes = runtimes?.Where(x => x == TargetRuntime).ToList().AsReadOnly();

        return runtimes ?? [];
    }
    
    HashSet<string> ListAllRuntimeIdentifiersInSolution()
    {
        var allRuntimes = Solution.AllProjects
                                  .SelectMany(p => p.GetRuntimeIdentifiers() ?? [])
                                  .Distinct()
                                  .Where(rid => rid != "win7-x86") //I have no idea where this is coming from, but let's ignore it. My theory is it's coming from the netstandard libs
                                  .ToHashSet();

        if (!string.IsNullOrWhiteSpace(TargetRuntime))
            allRuntimes = allRuntimes.Where(x => x == TargetRuntime).ToHashSet();

        return allRuntimes;
    }
    
    void SignDirectory(string directory)
    {
        if (!WillSignBinaries)
            return;

        Log.Information("Signing directory: {Directory} and sub-directories", directory);
        var binariesFolders = Directory.GetDirectories(directory, "*", new EnumerationOptions { RecurseSubdirectories = true });
        foreach (var subDirectory in binariesFolders.Append(directory))
        {
            Signing.SignAndTimestampBinaries(subDirectory, AzureKeyVaultUrl, AzureKeyVaultAppId,
                                             AzureKeyVaultAppSecret, AzureKeyVaultTenantId, AzureKeyVaultCertificateName,
                                             SigningCertificatePath, SigningCertificatePassword);
        }
    }
}
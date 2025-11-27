using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using Nuke.Common;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Tools.OctoVersion;
using Nuke.Common.Utilities.Collections;
using Octopus.Calamari.ConsolidatedPackage;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;
using static Nuke.Common.Tools.NuGet.NuGetTasks;

namespace Calamari.Build;

partial class Build : NukeBuild
{
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")] readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Required] readonly Solution Solution = SolutionModelTasks.ParseSolution(SourceDirectory / "Calamari.sln");

    [Parameter] readonly DotNetVerbosity BuildVerbosity = DotNetVerbosity.minimal;

    [Parameter] readonly bool SignBinaries;

    // When building locally signing isn't really necessary and it could take
    // up to 3-4 minutes to sign all the binaries as we build for many, many
    // different runtimes so disabling it locally means quicker turn around
    // when doing local development.
    bool WillSignBinaries => !IsLocalBuild || SignBinaries;

    [Parameter] readonly bool AppendTimestamp;

    [Parameter("Set Calamari Version on OctopusServer")] readonly bool SetOctopusServerVersion;

    [Parameter] readonly string? AzureKeyVaultUrl;

    [Parameter] readonly string? AzureKeyVaultAppId;

    [Parameter] [Secret] readonly string? AzureKeyVaultAppSecret;

    [Parameter] [Secret] readonly string? AzureKeyVaultTenantId;

    [Parameter] readonly string? AzureKeyVaultCertificateName;

    [Parameter(Name = "signing_certificate_path")] readonly string SigningCertificatePath = RootDirectory / "certificates" / "OctopusDevelopment.pfx";

    [Parameter(Name = "signing_certificate_password")] [Secret] readonly string SigningCertificatePassword = "Password01!";

    [Parameter] readonly string? TargetRuntime;

    const string CiBranchNameEnvVariable = "OCTOVERSION_CurrentBranch";

    [Parameter($"The name of the current git branch. OctoVersion will use this to calculate the version number. This can be set via the environment variable {CiBranchNameEnvVariable}.", Name = CiBranchNameEnvVariable)]
    string? BranchName { get; set; }

    //this is instantiated in the constructor
    readonly Lazy<OctoVersionInfo?> OctoVersionInfo;

    static readonly List<string> NuGetPackagesToExcludeFromConsolidation = new() { "Octopus.Calamari.CloudAccounts", "Octopus.Calamari.Common", "Octopus.Calamari.ConsolidateCalamariPackages", "Octopus.Calamari.ConsolidatedPackage", "Octopus.Calamari.ConsolidatedPackage.Api" };

    List<CalamariPackageMetadata> PackagesToPublish = new();
    List<Project> CalamariProjects = new();
    readonly List<Task> ProjectCompressionTasks = new();

    string ConsolidatedPackagePath = "";

    public Build()
    {
        // Mimic the behaviour of this attribute, but lazily so we don't pay the OctoVersion cost when it isn't needed
        OctoVersionInfo = new Lazy<OctoVersionInfo?>(() =>
                                                     {
                                                         var attribute = new OctoVersionAttribute { BranchMember = nameof(BranchName), Framework = "net8.0" };

                                                         // the Attribute does all the work such as calling TeamCity.Instance?.SetBuildNumber for us
                                                         var version = attribute.GetValue(null!, this);

                                                         return version as OctoVersionInfo;
                                                     }, LazyThreadSafetyMode.ExecutionAndPublication);

        NugetVersion = new Lazy<string>(GetNugetVersion);

        // This initialisation is required to ensure the build script can
        // perform actions such as GetRuntimeIdentifiers() on projects.
        ProjectModelTasks.Initialize();
    }

    static AbsolutePath SourceDirectory => RootDirectory / "source";
    static AbsolutePath BuildDirectory => RootDirectory / "build";
    static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    static AbsolutePath PublishDirectory => RootDirectory / "publish";
    static AbsolutePath LocalPackagesDirectory => RootDirectory / ".." / "LocalPackages";
    static AbsolutePath ConsolidateCalamariPackagesProject => SourceDirectory / "Calamari.ConsolidateCalamariPackages.Tests" / "Calamari.ConsolidateCalamariPackages.Tests.csproj";
    static AbsolutePath ConsolidatedPackageDirectory => ArtifactsDirectory / "consolidated";
    static AbsolutePath LegacyCalamariDirectory => PublishDirectory / "Calamari.Legacy";

    Lazy<string> NugetVersion { get; }

    Target CheckForbiddenWords =>
        d =>
            d.Executes(() =>
                       {
                           Log.Information("Checking codebase for forbidden words");

                           const string arguments =
                               "grep -i -I -n -f ForbiddenWords.txt -- \"./*\" \":(exclude)ForbiddenWords.txt\"";
                           var process =
                               ProcessTasks.StartProcess(GitPath, arguments)
                                           .AssertWaitForExit();

                           if (process.ExitCode == 1)
                           {
                               Log.Information("Sanity check passed");
                               return;
                           }

                           var filesContainingForbiddenWords = process.Output.Select(o => o.Text).ToArray();
                           if (filesContainingForbiddenWords.Any())
                               throw new Exception("Found forbidden words in the following files, "
                                                   + "please clean them up:\r\n"
                                                   + string.Join("\r\n", filesContainingForbiddenWords));
                       });

    Target Clean =>
        d =>
            d.Executes(() =>
                       {
                           SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(d => d.DeleteDirectory());
                           ArtifactsDirectory.CreateOrCleanDirectory();
                           PublishDirectory.CreateOrCleanDirectory();
                       });

    Target RestoreSolution =>
        d =>
            d.DependsOn(Clean)
             .Executes(() =>
                       {
                           var allRuntimeIds = ListAllRuntimeIdentifiersInSolution();
                           //we restore for all individual runtimes
                           foreach (var runtimeId in allRuntimeIds)
                           {
                               DotNetRestore(s => s.SetProjectFile(Solution).SetRuntime(runtimeId));
                           }
                       });

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
                           var globalSemaphore = new SemaphoreSlim(3);
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
                                                                                                                   .EnableSelfContained()
                                                                                                                   .SetVerbosity(projectName == "Calamari.Tests" ? DotNetVerbosity.detailed : DotNetVerbosity.minimal)));
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
                                              .SetSelfContained(OperatingSystem.IsWindows()) // This is here purely to make the local build experience on non-Windows devices workable - Publish breaks on non-Windows platforms with SelfContained = true
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

    Target PublishAzureWebAppNetCoreShim =>
        _ => _.DependsOn(RestoreSolution)
              .Executes(() =>
                        {
                            if (!OperatingSystem.IsWindows())
                            {
                                Log.Warning("Unable to build Calamari.AzureWebApp.NetCoreShim as it's a Full Framework application and can only be compiled on Windows");
                                return;
                            }

                            var project = Solution.GetProject("Calamari.AzureWebApp.NetCoreShim");
                            if (project is null)
                            {
                                Log.Error("Failed to find Calamari.AzureWebApp.NetCoreShim project");
                                return;
                            }

                            var outputPath = PublishDirectory / project.Name;

                            //as this is the only Net 4.6.2 application left, we do a build and restore here
                            DotNetPublish(s => s
                                               .SetConfiguration(Configuration)
                                               .SetProject(project.Path)
                                               .SetFramework("net462")
                                               .SetVersion(NugetVersion.Value)
                                               .SetInformationalVersion(OctoVersionInfo.Value?.InformationalVersion)
                                               .SetOutput(outputPath));

                            var archivePath = SourceDirectory / "Calamari.AzureWebApp" / "netcoreshim" / "netcoreshim.zip";
                            archivePath.DeleteFile();

                            outputPath.CompressTo(archivePath);
                        });

    Target CopyToLocalPackages =>
        d =>
            d.Requires(() => IsLocalBuild)
             .DependsOn(PublishCalamariProjects)
             .Executes(() =>
                       {
                           Directory.CreateDirectory(LocalPackagesDirectory);
                           foreach (AbsolutePath file in Directory.GetFiles(ArtifactsDirectory, "Octopus.Calamari.*.nupkg"))
                           {
                               var target = LocalPackagesDirectory / Path.GetFileName(file);
                               file.Copy(target, ExistsPolicy.FileOverwrite);
                           }
                       });

    Target PackageConsolidatedCalamariZip =>
        d =>
            d.DependsOn(PublishCalamariProjects)
             .Executes(() =>
                       {
                           var artifacts = Directory.GetFiles(ArtifactsDirectory, "*.nupkg")
                                                    .Where(a => !NuGetPackagesToExcludeFromConsolidation.Any(a.Contains));

                           var packageReferences = new List<BuildPackageReference>();
                           foreach (var artifact in artifacts)
                           {
                               using var zip = ZipFile.OpenRead(artifact);
                               var nuspecFileStream = zip.Entries.First(e => e.Name.EndsWith(".nuspec")).Open();
                               var nuspecReader = new NuspecReader(nuspecFileStream);
                               var metadata = nuspecReader.GetMetadata().ToList();
                               packageReferences.Add(new BuildPackageReference
                               {
                                   Name = Regex.Replace(metadata.Where(kvp => kvp.Key == "id").Select(i => i.Value).First(), @"^Octopus\.", ""),
                                   Version = metadata.Where(kvp => kvp.Key == "version").Select(i => i.Value).First(),
                                   PackagePath = artifact
                               });
                           }

                           foreach (var flavour in GetCalamariFlavours())
                           {
                               if (Solution.GetProject(flavour) != null)
                               {
                                   packageReferences.Add(new BuildPackageReference
                                   {
                                       Name = flavour,
                                       Version = NugetVersion.Value,
                                       PackagePath = ArtifactsDirectory / $"{flavour}.zip"
                                   });
                               }
                           }

                           Directory.CreateDirectory(ConsolidatedPackageDirectory);
                           var (result, packageFilename) = new Consolidate(Log.Logger).Execute(ConsolidatedPackageDirectory, packageReferences);

                           if (!result)
                               throw new Exception("Failed to consolidate calamari Packages");

                           ConsolidatedPackagePath = packageFilename;
                           Log.Information("Created consolidated package zip: {PackageFilename}", packageFilename);
                       });

    Target CalamariConsolidationVerification =>
        d =>
            d.DependsOn(PackageConsolidatedCalamariZip)
             .OnlyWhenDynamic(() => string.IsNullOrEmpty(TargetRuntime), "TargetRuntime is not restricted")
             .Executes(() =>
                       {
                           Environment.SetEnvironmentVariable("CONSOLIDATED_ZIP", ConsolidatedPackagePath);
                           Environment.SetEnvironmentVariable("EXPECTED_VERSION", NugetVersion.Value);
                           Environment.SetEnvironmentVariable("IS_WINDOWS", OperatingSystem.IsWindows().ToString());

                           DotNetTest(s => s
                                           .SetProjectFile(ConsolidateCalamariPackagesProject)
                                           .SetConfiguration(Configuration)
                                           .SetProcessArgumentConfigurator(args =>
                                                                               args.Add("--logger:\"console;verbosity=detailed\"")
                                                                                   .Add("--")
                                                                                   .Add("NUnit.ShowInternalProperties=true")));
                       });

    Target PackCalamariConsolidatedNugetPackage =>
        d =>
            d.DependsOn(CalamariConsolidationVerification)
             .Executes(() =>
                       {
                           NuGetPack(s => s.SetTargetPath(BuildDirectory / "Calamari.Consolidated.nuspec")
                                           .SetBasePath(BuildDirectory)
                                           .SetVersion(NugetVersion.Value)
                                           .SetOutputDirectory(ArtifactsDirectory));
                       });

    Target UpdateCalamariVersionOnOctopusServer =>
        d =>
            d.OnlyWhenStatic(() => SetOctopusServerVersion)
             .Requires(() => IsLocalBuild)
             .DependsOn(CopyToLocalPackages)
             .Executes(() =>
                       {
                           var serverProjectFile = RootDirectory / ".." / "OctopusDeploy" / "source" / "Octopus.Server" / "Octopus.Server.csproj";
                           if (File.Exists(serverProjectFile))
                           {
                               Log.Information("Setting Calamari version in Octopus Server "
                                               + "project {ServerProjectFile} to {NugetVersion}",
                                               serverProjectFile, NugetVersion.Value);
                               SetOctopusServerCalamariVersion(serverProjectFile);
                           }
                           else
                           {
                               Log.Warning("Could not set Calamari version in Octopus Server project "
                                           + "{ServerProjectFile} to {NugetVersion} as could not find "
                                           + "project file",
                                           serverProjectFile, NugetVersion.Value);
                           }
                       });

    Target SetTeamCityVersion => d => d.Executes(() => TeamCity.Instance?.SetBuildNumber(NugetVersion.Value));

    Target BuildLocal => d =>
                             d.DependsOn(PackCalamariConsolidatedNugetPackage)
                              .DependsOn(UpdateCalamariVersionOnOctopusServer);

    Target BuildCi => d =>
                          d.DependsOn(SetTeamCityVersion)
                           .DependsOn(PackCalamariConsolidatedNugetPackage);

    public static int Main() => Execute<Build>(x => IsServerBuild ? x.BuildCi : x.BuildLocal);

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

    // Sets the Octopus.Server.csproj Calamari.Consolidated package version
    void SetOctopusServerCalamariVersion(string projectFile)
    {
        var text = File.ReadAllText(projectFile);
        text = Regex.Replace(text, @"<BundledCalamariVersion>([\S])+</BundledCalamariVersion>",
                             $"<BundledCalamariVersion>{NugetVersion.Value}</BundledCalamariVersion>");
        File.WriteAllText(projectFile, text);
    }

    string GetNugetVersion()
    {
        return AppendTimestamp
            ? $"{OctoVersionInfo.Value?.NuGetVersion}-{DateTime.Now:yyyyMMddHHmmss}"
            : OctoVersionInfo.Value?.NuGetVersion
              ?? throw new InvalidOperationException("Unable to retrieve valid Nuget Version");
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

    // Although all libraries/flavours now support .NET Core, ServiceFabric can currently only be built on Windows devices
    // This is here purely to make the local build experience on non-Windows devices (with testing) workable
    static string[] GetCalamariFlavours()
    {
        return BuildableCalamariProjects.GetCalamariProjectsToBuild(OperatingSystem.IsWindows());
    }

    HashSet<string> ListAllRuntimeIdentifiersInSolution()
    {
        var allRuntimes = Solution.AllProjects
                                  .SelectMany(p => p.GetRuntimeIdentifiers() ?? Array.Empty<string>())
                                  .Distinct()
                                  .Where(rid => rid != "win7-x86") //I have no idea where this is coming from, but let's ignore it. My theory is it's coming from the netstandard libs
                                  .ToHashSet();

        if (!string.IsNullOrWhiteSpace(TargetRuntime))
            allRuntimes = allRuntimes.Where(x => x == TargetRuntime).ToHashSet();

        return allRuntimes;
    }
}

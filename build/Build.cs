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

namespace Calamari.Build
{
    partial class Build : NukeBuild
    {
        const string RootProjectName = "Calamari";

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

        [Parameter] readonly string? TargetFramework;

        [Parameter] readonly string? TargetRuntime;

        const string CiBranchNameEnvVariable = "OCTOVERSION_CurrentBranch";

        [Parameter($"The name of the current git branch. OctoVersion will use this to calculate the version number. This can be set via the environment variable {CiBranchNameEnvVariable}.", Name = CiBranchNameEnvVariable)]
        string? BranchName { get; set; }

        //this is instantiated in the constructor
        readonly Lazy<OctoVersionInfo?> OctoVersionInfo;

        static readonly List<string> CalamariProjectsToSkipConsolidation = new() { "Calamari.CloudAccounts", "Calamari.Common", "Calamari.ConsolidateCalamariPackages" };

        CalamariPackageMetadata[] PackagesToPublish = Array.Empty<CalamariPackageMetadata>();
        List<Project> CalamariProjects = new();
        readonly List<Task> ProjectCompressionTasks = new();

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

        static Target CheckForbiddenWords =>
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

        static Target Clean =>
            d =>
                d.Executes(() =>
                           {
                               SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(d => d.DeleteDirectory());
                               ArtifactsDirectory.CreateOrCleanDirectory();
                               PublishDirectory.CreateOrCleanDirectory();
                           });

        Target Restore =>
            d =>
                d.DependsOn(Clean)
                 .Executes(() =>
                           {
                               var localRuntime = FixedRuntimes.Windows;

                               if (!OperatingSystem.IsWindows())
                                   localRuntime = FixedRuntimes.Linux;

                               DotNetRestore(s => s.SetProjectFile(Solution)
                                                   .SetRuntime(localRuntime)
                                                   .SetProperty("DisableImplicitNuGetFallbackFolder", true));
                           });

        Target Compile =>
            d =>
                d.DependsOn(CheckForbiddenWords)
                 .DependsOn(Restore)
                 .Executes(() =>
                           {
                               Log.Information("Compiling Calamari v{CalamariVersion}", NugetVersion.Value);

                               DotNetBuild(s => s.SetProjectFile(Solution)
                                                 .SetConfiguration(Configuration)
                                                 .EnableNoRestore()
                                                 .SetVersion(NugetVersion.Value)
                                                 .SetInformationalVersion(OctoVersionInfo.Value?.InformationalVersion));
                           });

        Target CalamariConsolidationTests =>
            d =>
                d.DependsOn(Compile)
                 .OnlyWhenStatic(() => !IsLocalBuild)
                 .Executes(() =>
                           {
                               DotNetTest(s => s
                                               .SetProjectFile(ConsolidateCalamariPackagesProject)
                                               .SetConfiguration(Configuration)
                                               .EnableNoBuild());
                           });

        Target Publish =>
            d =>
                d.DependsOn(Compile)
                 .DependsOn(PublishAzureWebAppNetCoreShim)
                 .Executes(() =>
                           {
                               if (!OperatingSystem.IsWindows())
                                   Log.Warning("Building Calamari on a non-windows machine will result "
                                               + "in the {DefaultNugetPackageName} and {CloudNugetPackageName} "
                                               + "nuget packages being built as .Net Core 6.0 packages "
                                               + "instead of as .Net Framework. "
                                               + "This can cause compatibility issues when running certain "
                                               + "deployment steps in Octopus Server",
                                               RootProjectName, $"{RootProjectName}.{FixedRuntimes.Cloud}");

                               var nugetVersion = NugetVersion.Value;
                               var outputDirectory = DoPublish(RootProjectName,
                                                               OperatingSystem.IsWindows() ? Frameworks.Net462 : Frameworks.Net60,
                                                               nugetVersion);
                               if (OperatingSystem.IsWindows())
                               {
                                   outputDirectory.Copy(LegacyCalamariDirectory / RootProjectName, ExistsPolicy.DirectoryMerge | ExistsPolicy.FileFail);
                               }
                               else
                               {
                                   Log.Warning($"Skipping the bundling of {RootProjectName} into the Calamari.Legacy bundle. "
                                               + "This is required for providing .Net Framework executables for legacy Target Operating Systems");
                               }

                               DoPublish(RootProjectName,
                                         OperatingSystem.IsWindows() ? Frameworks.Net462 : Frameworks.Net60,
                                         nugetVersion,
                                         FixedRuntimes.Cloud);

                               foreach (var rid in GetRuntimeIdentifiers(Solution.GetProject(RootProjectName)!)!)
                                   DoPublish(RootProjectName, Frameworks.Net60, nugetVersion, rid);
                           });

        Target GetCalamariFlavourProjectsToPublish =>
            d =>
                d.DependsOn(Compile)
                 .Executes(() =>
                           {
                               var flavours = GetCalamariFlavours();
                               var migratedCalamariFlavoursTests = flavours.Select(f => $"{f}.Tests");
                               var calamariFlavourProjects = Solution.Projects
                                                                     .Where(project => flavours.Contains(project.Name)
                                                                                       || migratedCalamariFlavoursTests.Contains(project.Name));

                               // Calamari.Scripting is a library that other calamari flavours depend on; not a flavour on its own right.
                               // Unlike other *Calamari* tests, we would still want to produce Calamari.Scripting.Zip and its tests, like its flavours.
                               var calamariScripting = "Calamari.Scripting";
                               var calamariScriptingProjectAndTest = Solution.Projects.Where(project => project.Name == calamariScripting || project.Name == $"{calamariScripting}.Tests");

                               var calamariProjects = calamariFlavourProjects
                                                      .Concat(calamariScriptingProjectAndTest)
                                                      .ToList();

                               CalamariProjects = calamariProjects;

                               // All cross-platform Target Frameworks contain dots, all NetFx Target Frameworks don't
                               // eg: net40, net452, net48 vs netcoreapp3.1, net5.0, net6.0
                               bool IsCrossPlatform(string targetFramework) => targetFramework.Contains('.');

                               var calamariPackages =
                                   calamariProjects.SelectMany(project => project.GetTargetFrameworks()!, (p, f) => new
                                                   {
                                                       Project = p,
                                                       Framework = f,
                                                       CrossPlatform = IsCrossPlatform(f)
                                                   })
                                                   .ToList();

                               // for NetFx target frameworks, we use "netfx" as the architecture, and ignore defined runtime identifiers
                               var netFxPackages =
                                   calamariPackages.Where(p => !p.CrossPlatform)
                                                   .Select(packageToBuild => new CalamariPackageMetadata()
                                                   {
                                                       Project = packageToBuild.Project,
                                                       Framework = packageToBuild.Framework,
                                                       Architecture = null,
                                                       IsCrossPlatform = packageToBuild.CrossPlatform
                                                   });

                               // for cross-platform frameworks, we combine each runtime identifier with each target framework
                               var crossPlatformPackages =
                                   calamariPackages.Where(p => p.CrossPlatform)
                                                   .Where(p => string.IsNullOrWhiteSpace(TargetFramework) || p.Framework == TargetFramework)
                                                   .SelectMany(packageToBuild => GetRuntimeIdentifiers(packageToBuild.Project) ?? Enumerable.Empty<string>(),
                                                               (packageToBuild, runtimeIdentifier) => new CalamariPackageMetadata()
                                                               {
                                                                   Project = packageToBuild.Project,
                                                                   Framework = packageToBuild.Framework,
                                                                   Architecture = runtimeIdentifier,
                                                                   IsCrossPlatform = packageToBuild.CrossPlatform
                                                               })
                                                   .Distinct(t => new { t.Project?.Name, t.Architecture, t.Framework });

                               PackagesToPublish = crossPlatformPackages.Concat(netFxPackages).ToArray();
                           });

        Target RestoreCalamariProjects =>
            d =>
                d.DependsOn(GetCalamariFlavourProjectsToPublish)
                 .Executes(() =>
                           {
                               PackagesToPublish
                                   .Select(p => p.Architecture)
                                   .Distinct()
                                   .ForEach(rid => DotNetRestore(s =>
                                                                     s.SetProjectFile(Solution)
                                                                      .SetProperty("DisableImplicitNuGetFallbackFolder", true)
                                                                      .SetRuntime(rid)));
                           });

        Target BuildCalamariProjects =>
            d =>
                d.DependsOn(RestoreCalamariProjects)
                 .Executes(async () =>
                           {
                               var globalSemaphore = new SemaphoreSlim(3);
                               var semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

                               var buildTasks = PackagesToPublish.Select(async calamariPackageMetadata =>
                                                                         {
                                                                             if (!OperatingSystem.IsWindows() && !calamariPackageMetadata.IsCrossPlatform)
                                                                             {
                                                                                 Log.Warning($"Not Building {calamariPackageMetadata.Framework}: can only publish netfx on a Windows OS");
                                                                                 return;
                                                                             }

                                                                             var projectName = calamariPackageMetadata.Project?.Name ?? throw new Exception("Could not find project name");
                                                                             var projectSemaphore = semaphores.GetOrAdd(projectName, _ => new SemaphoreSlim(1, 1));

                                                                             // for NetFx target frameworks, we use "netfx" as the architecture, and ignore defined runtime identifiers, here we'll just block on all netfx
                                                                             var architectureSemaphore = semaphores.GetOrAdd(calamariPackageMetadata.Architecture ?? "Unknown Architecture", _ => new SemaphoreSlim(1, 1));

                                                                             await globalSemaphore.WaitAsync();
                                                                             await projectSemaphore.WaitAsync();
                                                                             await architectureSemaphore.WaitAsync();
                                                                             try
                                                                             {
                                                                                 Log.Information($"Building {calamariPackageMetadata.Project?.Name} for framework '{calamariPackageMetadata.Framework}' and arch '{calamariPackageMetadata.Architecture}'");

                                                                                 await Task.Run(() =>
                                                                                                    DotNetBuild(s =>
                                                                                                                    s.SetProjectFile(calamariPackageMetadata.Project)
                                                                                                                     .SetConfiguration(Configuration)
                                                                                                                     .SetFramework(calamariPackageMetadata.Framework)
                                                                                                                     .EnableNoRestore()
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
                               var signTasks = outputPaths
                                               .Where(output => output != null && !output.ToString().Contains("Tests"))
                                               .Select(output => Task.Run(() => SignDirectory(output!)))
                                               .ToList();

                               await Task.WhenAll(signTasks);
                               StageLegacyCalamariAssemblies(PackagesToPublish);
                               CalamariProjects.ForEach(CompressCalamariProject);
                               await Task.WhenAll(ProjectCompressionTasks);
                           });

        async Task<AbsolutePath?> PublishPackageAsync(CalamariPackageMetadata calamariPackageMetadata)
        {
            if (!OperatingSystem.IsWindows() && !calamariPackageMetadata.IsCrossPlatform)
            {
                Log.Warning($"Not publishing {calamariPackageMetadata.Framework}: can only publish netfx on a Windows OS");
                return null;
            }

            Log.Information($"Publishing {calamariPackageMetadata.Project?.Name} for framework '{calamariPackageMetadata.Framework}' and arch '{calamariPackageMetadata.Architecture}'");

            var project = calamariPackageMetadata.Project;
            var outputDirectory = PublishDirectory / project?.Name / (calamariPackageMetadata.IsCrossPlatform ? calamariPackageMetadata.Architecture : "netfx");

            await Task.Run(() =>
                               DotNetPublish(s =>
                                                 s.SetConfiguration(Configuration)
                                                  .SetProject(project)
                                                  .SetFramework(calamariPackageMetadata.Framework)
                                                  .SetRuntime(calamariPackageMetadata.Architecture)
                                                  .EnableNoBuild()
                                                  .EnableNoRestore()
                                                  .EnableSelfContained()
                                                  .SetOutput(outputDirectory)));

            File.Copy(RootDirectory / "global.json", outputDirectory / "global.json");

            return outputDirectory;
        }

        static void StageLegacyCalamariAssemblies(CalamariPackageMetadata[] packagesToPublish)
        {
            if (!OperatingSystem.IsWindows())
            {
                Log.Warning($"Skipping the bundling of Calamari projects into the Calamari.Legacy bundle. "
                            + "This is required for providing .Net Framework executables for legacy Target Operating Systems.");
                return;
            }

            packagesToPublish
                //We only need to bundle executable (not tests or libraries) full framework projects 
                .Where(d => d.Framework == Frameworks.Net462 && d.Project.GetOutputType() == "Exe")
                .ForEach(calamariPackageMetadata =>
                         {
                             Log.Information($"Copying {calamariPackageMetadata.Project?.Name} for legacy Calamari '{calamariPackageMetadata.Framework}' and arch '{calamariPackageMetadata.Architecture}'");
                             var project = calamariPackageMetadata.Project;
                             if (project is not null)
                             {
                                 var publishedPath = PublishDirectory / project.Name / "netfx";
                                 publishedPath.Copy(LegacyCalamariDirectory / project.Name, ExistsPolicy.DirectoryMerge | ExistsPolicy.FileFail);
                             }
                         });
        }

        void CompressCalamariProject(Project project)
        {
            Log.Verbose($"Compressing Calamari flavour {PublishDirectory}/{project.Name}");
            var compressionSource = PublishDirectory / project.Name;
            if (!Directory.Exists(compressionSource))
            {
                Log.Verbose($"Skipping compression for {project.Name} since nothing was built");
                return;
            }

            var compressionTask = Task.Run(() => compressionSource.CompressTo($"{ArtifactsDirectory / project.Name}.zip"));
            ProjectCompressionTasks.Add(compressionTask);
        }

        Target PublishAzureWebAppNetCoreShim =>
            _ => _.DependsOn(Restore)
                  .Executes(() =>
                            {
                                if (!OperatingSystem.IsWindows())
                                {
                                    Log.Warning("Unable to build Calamari.AzureWebApp.NetCoreShim as it's a Full Framework application and can only be compiled on Windows");
                                    return;
                                }

                                var project = Solution.GetProject("Calamari.AzureWebApp.NetCoreShim");

                                var outputPath = PublishDirectory / project.Name;

                                DotNetPublish(s => s
                                                   .SetConfiguration(Configuration)
                                                   .SetProject(project.Path)
                                                   .SetFramework(Frameworks.Net462)
                                                   .EnableNoRestore()
                                                   .SetVersion(NugetVersion.Value)
                                                   .SetInformationalVersion(OctoVersionInfo.Value?.InformationalVersion)
                                                   .SetOutput(outputPath));

                                var archivePath = SourceDirectory / "Calamari.AzureWebApp" / "netcoreshim" / "netcoreshim.zip";
                                archivePath.DeleteFile();

                                outputPath.CompressTo(archivePath);
                            });

        Target PackLegacyCalamari =>
            d =>
                d.DependsOn(Publish)
                 .DependsOn(PublishCalamariProjects)
                 .Executes(() =>
                           {
                               if (!OperatingSystem.IsWindows())
                               {
                                   return;
                               }

                               Log.Verbose($"Compressing Calamari.Legacy");
                               LegacyCalamariDirectory.ZipTo(ArtifactsDirectory / $"Calamari.Legacy.{NugetVersion.Value}.zip");
                           });

        Target PackBinaries =>
            d =>
                d.DependsOn(Publish)
                 .DependsOn(PublishCalamariProjects)
                 .Executes(async () =>
                           {
                               var nugetVersion = NugetVersion.Value;
                               var packageActions = new List<Action>
                               {
                                   () => DoPackage(RootProjectName,
                                                   OperatingSystem.IsWindows() ? Frameworks.Net462 : Frameworks.Net60,
                                                   nugetVersion),
                                   () => DoPackage(RootProjectName,
                                                   OperatingSystem.IsWindows() ? Frameworks.Net462 : Frameworks.Net60,
                                                   nugetVersion,
                                                   FixedRuntimes.Cloud),
                               };

                               // Create the self-contained Calamari packages for each runtime ID defined in Calamari.csproj
                               // ReSharper disable once LoopCanBeConvertedToQuery
                               foreach (var rid in GetRuntimeIdentifiers(Solution.GetProject(RootProjectName)!)!)
                                   packageActions.Add(() => DoPackage(RootProjectName,
                                                                      Frameworks.Net60,
                                                                      nugetVersion,
                                                                      rid));

                               var dotNetCorePackSettings = new DotNetPackSettings().SetConfiguration(Configuration)
                                                                                    .SetOutputDirectory(ArtifactsDirectory)
                                                                                    .EnableNoBuild()
                                                                                    .EnableIncludeSource()
                                                                                    .SetVersion(nugetVersion)
                                                                                    .EnableNoRestore();

                               var commonProjects = Directory.GetFiles(SourceDirectory, "*.Common.csproj",
                                                                       new EnumerationOptions { RecurseSubdirectories = true });

                               // ReSharper disable once LoopCanBeConvertedToQuery
                               foreach (var project in commonProjects)
                                   packageActions.Add(() => SignAndPack(project.ToString(), dotNetCorePackSettings));

                               // Pack the Consolidation Libraries
                               var consolidateCalamariPackagesProjectPrefix = "Calamari.ConsolidateCalamariPackages";
                               Solution.Projects.Where(project => project.Name.StartsWith(consolidateCalamariPackagesProjectPrefix)).ForEach(p => packageActions.Add(() => SignAndPack(p, dotNetCorePackSettings)));

                               var sourceProjectPath =
                                   SourceDirectory / "Calamari.CloudAccounts" / "Calamari.CloudAccounts.csproj";
                               packageActions.Add(() => SignAndPack(sourceProjectPath,
                                                                    dotNetCorePackSettings));

                               await RunPackActions(packageActions);
                           });

        Target PackTests =>
            d =>
                d.DependsOn(Publish)
                 .DependsOn(PublishCalamariProjects)
                 .Executes(async () =>
                           {
                               var nugetVersion = NugetVersion.Value;
                               var defaultTarget = OperatingSystem.IsWindows() ? Frameworks.Net462 : Frameworks.Net60;
                               AbsolutePath binFolder = SourceDirectory / "Calamari.Tests" / "bin" / Configuration / defaultTarget;
                               Directory.Exists(binFolder);
                               var actions = new List<Action>
                               {
                                   () => binFolder.CompressTo( ArtifactsDirectory / "Binaries.zip")
                               };

                               // Create a Zip for each runtime for testing
                               // ReSharper disable once LoopCanBeConvertedToQuery
                               actions.Add(() =>
                                           {
                                               //run each build in sequence as it's the same project and we get issues
                                               foreach (var rid in GetRuntimeIdentifiers(Solution.GetProject("Calamari.Tests")!))
                                               {
                                                   var publishedLocation = DoPublish("Calamari.Tests", Frameworks.Net60, nugetVersion, rid);
                                                   var zipName = $"Calamari.Tests.{rid}.{nugetVersion}.zip";
                                                   File.Copy(RootDirectory / "global.json", publishedLocation / "global.json");
                                                   publishedLocation.CompressTo(ArtifactsDirectory / zipName);
                                               }
                                           });

                               //I don't think this is _actually_ necessary to build...
                               actions.Add(() =>
                                           {
                                               var testingProjectPath = SourceDirectory / "Calamari.Testing" / "Calamari.Testing.csproj";
                                               DotNetPack(new DotNetPackSettings().SetConfiguration(Configuration)
                                                                                  .SetProject(testingProjectPath)
                                                                                  .SetOutputDirectory(ArtifactsDirectory)
                                                                                  .EnableNoBuild()
                                                                                  .EnableIncludeSource()
                                                                                  .SetVersion(nugetVersion)
                                                                                  .EnableNoRestore());
                                           });

                               await RunPackActions(actions);
                           });

        Target Pack =>
            d =>
                d.DependsOn(PackTests)
                 .DependsOn(PackBinaries)
                 .DependsOn(PackLegacyCalamari);

        Target CopyToLocalPackages =>
            d =>
                d.Requires(() => IsLocalBuild)
                 .DependsOn(PackBinaries)
                 .Executes(() =>
                           {
                               Directory.CreateDirectory(LocalPackagesDirectory);
                               var calamariNupkgs = Directory.GetFiles(ArtifactsDirectory, "Calamari.*.nupkg");
                               var octopusCalamariNpkgs = Directory.GetFiles(ArtifactsDirectory, "Octopus.Calamari.*.nupkg");

                               foreach (AbsolutePath file in calamariNupkgs.Concat(octopusCalamariNpkgs).Where(f => f != null))
                               {
                                   file.Copy(LocalPackagesDirectory / Path.GetFileName(file), ExistsPolicy.FileOverwrite);
                               }
                                   
                           });

        Target PackageConsolidatedCalamariZip =>
            d =>
                d.DependsOn(CalamariConsolidationTests)
                 .DependsOn(PackBinaries)
                 .Executes(() =>
                           {
                               var artifacts = Directory.GetFiles(ArtifactsDirectory, "*.nupkg")
                                                        .Where(a => !CalamariProjectsToSkipConsolidation.Any(a.Contains));
                               var packageReferences = new List<BuildPackageReference>();
                               foreach (var artifact in artifacts)
                               {
                                   using var zip = ZipFile.OpenRead(artifact);
                                   var nuspecFileStream = zip.Entries.First(e => e.Name.EndsWith(".nuspec")).Open();
                                   var nuspecReader = new NuspecReader(nuspecFileStream);
                                   var metadata = nuspecReader.GetMetadata().ToList();
                                   packageReferences.Add(new BuildPackageReference
                                   {
                                       Name = metadata.Where(kvp => kvp.Key == "id").Select(i => i.Value).First(),
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

                               Log.Information($"Created consolidated package zip: {packageFilename}");
                           });

        Target PackCalamariConsolidatedNugetPackage =>
            d =>
                d.DependsOn(PackageConsolidatedCalamariZip)
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
                               .DependsOn(Pack)
                               .DependsOn(PackCalamariConsolidatedNugetPackage);

        public static int Main() => Execute<Build>(x => IsServerBuild ? x.BuildCi : x.BuildLocal);

        async Task RunPackActions(List<Action> actions)
        {
            var tasks = actions.Select(Task.Run).ToList();
            await Task.WhenAll(tasks);
        }

        AbsolutePath DoPublish(string project, string framework, string version, string? runtimeId = null)
        {
            var publishedTo = PublishDirectory / project / framework;

            if (!runtimeId.IsNullOrEmpty())
            {
                publishedTo /= runtimeId;
                runtimeId = runtimeId != "portable" && runtimeId != "Cloud" ? runtimeId : null;
            }

            DotNetPublish(s =>
                              s.SetProject(Solution.GetProject(project))
                               .SetConfiguration(Configuration)
                               .SetOutput(publishedTo)
                               .SetFramework(framework)
                               .SetVersion(NugetVersion.Value)
                               .SetVerbosity(BuildVerbosity)
                               .SetRuntime(runtimeId)
                               .SetVersion(version)
                               .EnableSelfContained()
                         );

            if (WillSignBinaries)
            {
                Signing.SignAndTimestampBinaries(publishedTo, AzureKeyVaultUrl, AzureKeyVaultAppId,
                                                 AzureKeyVaultAppSecret, AzureKeyVaultTenantId, AzureKeyVaultCertificateName,
                                                 SigningCertificatePath, SigningCertificatePassword);
            }

            return publishedTo;
        }

        void SignProject(string project)
        {
            if (!WillSignBinaries)
                return;
            var binDirectory = $"{Path.GetDirectoryName(project)}/bin/{Configuration}/";
            SignDirectory(binDirectory);
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

        void SignAndPack(string project, DotNetPackSettings dotNetCorePackSettings)
        {
            Log.Information("SignAndPack project: {Project}", project);
            SignProject(project);
            DotNetPack(dotNetCorePackSettings.SetProject(project));
        }

        void DoPackage(string project, string framework, string version, string? runtimeId = null)
        {
            var publishedTo = PublishDirectory / project / framework;
            var projectDir = SourceDirectory / project;
            var packageId = $"{project}";
            var nugetPackProperties = new Dictionary<string, object>();

            if (!runtimeId.IsNullOrEmpty())
            {
                publishedTo /= runtimeId;
                packageId = $"{project}.{runtimeId}";
                nugetPackProperties.Add("runtimeId", runtimeId!);
            }

            if (WillSignBinaries)
                Signing.SignAndTimestampBinaries(publishedTo, AzureKeyVaultUrl, AzureKeyVaultAppId,
                                                 AzureKeyVaultAppSecret, AzureKeyVaultTenantId, AzureKeyVaultCertificateName,
                                                 SigningCertificatePath, SigningCertificatePassword);

            AbsolutePath nuspecSrc = $"{projectDir}/{project}.nuspec";
            AbsolutePath nuspecDest = $"{publishedTo}/{packageId}.nuspec";
            nuspecSrc.Copy(nuspecDest, ExistsPolicy.FileOverwrite);
            var text = File.ReadAllText(nuspecDest);
            text = text.Replace("$id$", packageId)
                       .Replace("$version$", version);
            File.WriteAllText(nuspecDest, text);

            NuGetPack(s =>
                          s.SetBasePath(publishedTo)
                           .SetOutputDirectory(ArtifactsDirectory)
                           .SetTargetPath(nuspecDest)
                           .SetVersion(NugetVersion.Value)
                           .SetVerbosity(NuGetVerbosity.Normal)
                           .SetProperties(nugetPackProperties));
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
                return Array.Empty<string>();

            var runtimes = project.GetRuntimeIdentifiers();

            if (!string.IsNullOrWhiteSpace(TargetRuntime))
                runtimes = runtimes?.Where(x => x == TargetRuntime).ToList().AsReadOnly();

            return runtimes ?? Array.Empty<string>();
        }

        //All libraries/flavours now support .NET Core
        static List<string> GetCalamariFlavours() => CalamariPackages.Flavours;
    }
}
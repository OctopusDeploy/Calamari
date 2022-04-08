using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;

namespace Calamari.Build
{
    class Build : NukeBuild
    {
        const string RootProjectName = "Calamari";

        [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
        readonly Configuration Configuration =
            IsLocalBuild ? Configuration.Debug : Configuration.Release;

        [Solution] 
        [Required] 
        readonly Solution? Solution;

        [Parameter("Run packing step in parallel")] 
        readonly bool PackInParallel;

        [Parameter] 
        readonly DotNetVerbosity BuildVerbosity = DotNetVerbosity.Minimal;

        [Parameter] 
        readonly bool SignBinaries;
        
        // When building locally signing isn't really necessary and it could take
        // up to 3-4 minutes to sign all the binaries as we build for many, many
        // different runtimes so disabling it locally means quicker turn around
        // when doing local development.
        bool WillSignBinaries => !IsLocalBuild || SignBinaries;

        [Parameter] 
        readonly bool AppendTimestamp;

        [Parameter("Set Calamari Version on OctopusServer")] 
        readonly bool SetOctopusServerVersion;

        [Parameter] 
        readonly string? AzureKeyVaultUrl;

        [Parameter] 
        readonly string? AzureKeyVaultAppId;

        [Parameter]
        [Secret]
        readonly string? AzureKeyVaultAppSecret;

        [Parameter] 
        readonly string? AzureKeyVaultCertificateName;

        [Parameter(Name = "signing_certificate_path")]
        readonly string SigningCertificatePath = "./certificates/OctopusDevelopment.pfx";

        [Parameter(Name = "signing_certificate_password")] 
        [Secret]
        readonly string SigningCertificatePassword = "Password01!";

        [Required]
        [GitVersion]
        readonly GitVersion? GitVersionInfo;

        public Build()
        {
            NugetVersion = new Lazy<string>(GetNugetVersion); 
            
            // This initialisation is required to ensure the build script can
            // perform actions such as GetRuntimeIdentifiers() on projects.
            ProjectModelTasks.Initialize();
        }

        static AbsolutePath SourceDirectory => RootDirectory / "source";
        static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
        static AbsolutePath PublishDirectory => RootDirectory / "publish";
        static AbsolutePath LocalPackagesDirectory => RootDirectory / "../LocalPackages";

        Lazy<string> NugetVersion { get; }

        Target CheckForbiddenWords =>
            _ => _.Executes(() =>
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
            _ => _.Executes(() =>
            {
                SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(DeleteDirectory);
                EnsureCleanDirectory(ArtifactsDirectory);
                EnsureCleanDirectory(PublishDirectory);
            });

        Target Restore =>
            _ => _.DependsOn(Clean)
                  .Executes(() =>
                  {
                      var localRuntime = FixedRuntimes.Windows;

                      if (!OperatingSystem.IsWindows())
                          localRuntime = FixedRuntimes.Linux;

                      DotNetRestore(_ => _.SetProjectFile(Solution)
                                          .SetRuntime(localRuntime)
                                          .SetProperty("DisableImplicitNuGetFallbackFolder", true));
                  });

        Target Compile =>
            _ => _.DependsOn(CheckForbiddenWords)
                  .DependsOn(Restore)
                  .Executes(() =>
                  {
                      Log.Information("Compiling Calamari v{CalamariVersion}", NugetVersion.Value);
                      
                      DotNetBuild(_ => _.SetProjectFile(Solution)
                                        .SetConfiguration(Configuration)
                                        .SetNoRestore(true)
                                        .SetVersion(NugetVersion.Value)
                                        .SetInformationalVersion(GitVersionInfo?.InformationalVersion));
                  });

        Target Publish =>
            _ => _.DependsOn(Compile)
                  .Executes(() =>
                  {
                      if (!OperatingSystem.IsWindows())
                          Log.Warning("Building Calamari on a non-windows machine will result "
                                      + "in the {DefaultNugetPackageName} and {CloudNugetPackageName} "
                                      + "nuget packages being built as .Net Core 3.1 packages "
                                      + "instead of as .Net Framework 4.0 and 4.5.2 respectively. "
                                      + "This can cause compatibility issues when running certain "
                                      + "deployment steps in Octopus Server",
                                      RootProjectName, $"{RootProjectName}.{FixedRuntimes.Cloud}");

                      var nugetVersion = NugetVersion.Value;
                      DoPublish(RootProjectName,
                                OperatingSystem.IsWindows() ? Frameworks.Net40 : Frameworks.NetCoreApp31,
                                nugetVersion);
                      DoPublish(RootProjectName,
                                OperatingSystem.IsWindows() ? Frameworks.Net452 : Frameworks.NetCoreApp31,
                                nugetVersion,
                                FixedRuntimes.Cloud);

                      DoPublish(RootProjectName, Frameworks.NetCoreApp31, nugetVersion, FixedRuntimes.Portable);

                      // Create the self-contained Calamari packages for each runtime ID defined in Calamari.csproj
                      foreach (var rid in Solution?.GetProject(RootProjectName).GetRuntimeIdentifiers()!)
                          DoPublish(RootProjectName, Frameworks.NetCoreApp31, nugetVersion, rid);
                  });

        Target PackBinaries =>
            _ => _.DependsOn(Publish)
                  .Executes(async () =>
                  {
                      var nugetVersion = NugetVersion.Value;
                      var packageActions = new List<Action>
                      {
                          () => DoPackage(RootProjectName,
                                          OperatingSystem.IsWindows() ? Frameworks.Net40 : Frameworks.NetCoreApp31,
                                          nugetVersion),
                          () => DoPackage(RootProjectName,
                                          OperatingSystem.IsWindows() ? Frameworks.Net452 : Frameworks.NetCoreApp31,
                                          nugetVersion,
                                          FixedRuntimes.Cloud),
                          // Create a portable .NET Core package
                          () => DoPackage(RootProjectName, Frameworks.NetCoreApp31, nugetVersion,
                                          FixedRuntimes.Portable)
                      };

                      // Create the self-contained Calamari packages for each runtime ID defined in Calamari.csproj
                      // ReSharper disable once LoopCanBeConvertedToQuery
                      foreach (var rid in Solution?.GetProject(RootProjectName).GetRuntimeIdentifiers()!)
                          packageActions.Add(() => DoPackage(RootProjectName,
                                                             Frameworks.NetCoreApp31,
                                                             nugetVersion,
                                                             rid));

                      var dotNetCorePackSettings = new DotNetPackSettings().SetConfiguration(Configuration)
                                                                           .SetOutputDirectory(ArtifactsDirectory)
                                                                           .EnableNoBuild()
                                                                           .EnableIncludeSource()
                                                                           .SetVersion(nugetVersion)
                                                                           .SetNoRestore(true);

                      var commonProjects = Directory.GetFiles(SourceDirectory, "*.Common.csproj",
                                                              new EnumerationOptions { RecurseSubdirectories = true });

                      // ReSharper disable once LoopCanBeConvertedToQuery
                      foreach (var project in commonProjects)
                          packageActions.Add(() => SignAndPack(project.ToString(), dotNetCorePackSettings));

                      var sourceProjectPath =
                          SourceDirectory / "Calamari.CloudAccounts" / "Calamari.CloudAccounts.csproj";
                      packageActions.Add(() => SignAndPack(sourceProjectPath,
                                                           dotNetCorePackSettings));

                      await RunPackActions(packageActions);
                  });

        Target PackTests =>
            _ => _.DependsOn(Publish)
                  .Executes(async () =>
                  {
                      var nugetVersion = NugetVersion.Value;
                      var defaultTarget = OperatingSystem.IsWindows() ? Frameworks.Net461 : Frameworks.NetCoreApp31;
                      var binFolder = $"./source/Calamari.Tests/bin/{Configuration}/{defaultTarget}/";
                      Directory.Exists(binFolder);
                      var actions = new List<Action>
                      {
                          () =>
                          {
                              CompressionTasks.Compress(binFolder, ArtifactsDirectory / "Binaries.zip");
                          }
                      };

                      // Create a Zip for each runtime for testing
                      // ReSharper disable once LoopCanBeConvertedToQuery
                      foreach (var rid in Solution?.GetProject("Calamari.Tests").GetRuntimeIdentifiers()!)
                          actions.Add(() =>
                          {
                              var publishedLocation =
                                  DoPublish("Calamari.Tests", Frameworks.NetCoreApp31, nugetVersion, rid);
                              var zipName = $"Calamari.Tests.netcoreapp.{rid}.{nugetVersion}.zip";
                              CompressionTasks.Compress(publishedLocation, ArtifactsDirectory / zipName);
                          });

                      actions.Add(() =>
                      {
                          var testingProjectPath = SourceDirectory / "Calamari.Testing" / "Calamari.Testing.csproj";
                          DotNetPack(new DotNetPackSettings().SetConfiguration(Configuration)
                                                             .SetProject(testingProjectPath)
                                                             .SetOutputDirectory(ArtifactsDirectory)
                                                             .EnableNoBuild()
                                                             .EnableIncludeSource()
                                                             .SetVersion(nugetVersion)
                                                             .SetNoRestore(true));
                      });

                      await RunPackActions(actions);
                  });

        Target Pack =>
            _ => _.DependsOn(PackTests)
                  .DependsOn(PackBinaries);

        Target CopyToLocalPackages =>
            _ => _.Requires(() => IsLocalBuild)
                  .DependsOn(PackBinaries)
                  .Executes(() =>
                  {
                      Directory.CreateDirectory(LocalPackagesDirectory);
                      foreach (var file in Directory.GetFiles(ArtifactsDirectory, "Calamari.*.nupkg"))
                          CopyFile(file, LocalPackagesDirectory / Path.GetFileName(file), FileExistsPolicy.Overwrite);
                  });

        Target UpdateCalamariVersionOnOctopusServer =>
            _ =>
                _.Requires(() => SetOctopusServerVersion)
                 .Requires(() => IsLocalBuild)
                 .DependsOn(CopyToLocalPackages)
                 .Executes(() =>
                 {
                     var serverProjectFile =
                         Path.GetFullPath("../OctopusDeploy/source/Octopus.Server/Octopus.Server.csproj");
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

        Target SetTeamCityVersion => _ => _.Executes(() =>
        {
            TeamCity.Instance?.SetBuildNumber(NugetVersion.Value);
        });

        Target BuildLocal => _ => _.DependsOn(PackBinaries)
                                   .DependsOn(CopyToLocalPackages)
                                   .DependsOn(UpdateCalamariVersionOnOctopusServer);

        Target BuildCi => _ => _.DependsOn(SetTeamCityVersion)
                                .DependsOn(Pack);

        public static int Main() => Execute<Build>(x => IsServerBuild ? x.BuildCi : x.BuildLocal);

        async Task RunPackActions(List<Action> actions)
        {
            if (PackInParallel)
            {
                var tasks = actions.Select(Task.Run).ToList();
                await Task.WhenAll(tasks);
            }
            else
            {
                foreach (var action in actions)
                    action();
            }
        }

        AbsolutePath DoPublish(string project, string framework, string version, string? runtimeId = null)
        {
            var publishedTo = PublishDirectory / project / framework;

            if (!runtimeId.IsNullOrEmpty())
            {
                publishedTo /= runtimeId;
                runtimeId = runtimeId != "portable" && runtimeId != "Cloud" ? runtimeId : null;
            }

            DotNetPublish(_ => _.SetProject(Solution?.GetProject(project))
                                .SetConfiguration(Configuration)
                                .SetOutput(publishedTo)
                                .SetFramework(framework)
                                .SetVersion(NugetVersion.Value)
                                .SetVerbosity(BuildVerbosity)
                                .SetRuntime(runtimeId)
                                .SetVersion(version));

            if (WillSignBinaries)
                Signing.SignAndTimestampBinaries(publishedTo, AzureKeyVaultUrl, AzureKeyVaultAppId,
                                                 AzureKeyVaultAppSecret, AzureKeyVaultCertificateName,
                                                 SigningCertificatePath, SigningCertificatePassword);

            return publishedTo;
        }

        void SignAndPack(string project, DotNetPackSettings dotNetCorePackSettings)
        {
            Log.Information("SignAndPack project: {Project}", project);

            if (WillSignBinaries)
            {
                var binDirectory = $"{Path.GetDirectoryName(project)}/bin/{Configuration}/";
                var binariesFolders =
                    Directory.GetDirectories(binDirectory, "*", new EnumerationOptions { RecurseSubdirectories = true });
                foreach (var directory in binariesFolders)
                    Signing.SignAndTimestampBinaries(directory, AzureKeyVaultUrl, AzureKeyVaultAppId,
                                                     AzureKeyVaultAppSecret, AzureKeyVaultCertificateName,
                                                     SigningCertificatePath, SigningCertificatePassword);
            }
            
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
                                                 AzureKeyVaultAppSecret, AzureKeyVaultCertificateName,
                                                 SigningCertificatePath, SigningCertificatePassword);

            var nuspec = $"{publishedTo}/{packageId}.nuspec";
            CopyFile($"{projectDir}/{project}.nuspec", nuspec, FileExistsPolicy.Overwrite);
            var text = File.ReadAllText(nuspec);
            text = text.Replace("$id$", packageId)
                       .Replace("$version$", version);
            File.WriteAllText(nuspec, text);

            NuGetTasks.NuGetPack(_ => _.SetBasePath(publishedTo)
                                       .SetOutputDirectory(ArtifactsDirectory)
                                       .SetTargetPath(nuspec)
                                       .SetVersion(NugetVersion.Value)
                                       .SetVerbosity(NuGetVerbosity.Normal)
                                       .SetProperties(nugetPackProperties));
        }

        // Sets the Octopus.Server.csproj <CalamariVersion> property
        void SetOctopusServerCalamariVersion(string projectFile)
        {
            var text = File.ReadAllText(projectFile);
            text = Regex.Replace(text, @"<CalamariVersion>([\S])+<\/CalamariVersion>",
                                 $"<CalamariVersion>{NugetVersion.Value}</CalamariVersion>");
            File.WriteAllText(projectFile, text);
        }

        string GetNugetVersion()
        {
            return AppendTimestamp
                ? $"{GitVersionInfo?.NuGetVersion}-{DateTime.Now:yyyyMMddHHmmss}"
                : GitVersionInfo?.NuGetVersion
                  ?? throw new InvalidOperationException("Unable to retrieve valid Nuget Version");
        }
    }
}
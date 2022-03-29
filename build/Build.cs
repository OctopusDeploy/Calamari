using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.AzureSignTool;
using Nuke.Common.Tools.OctoVersion;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Tools.SignTool;
using Serilog;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;
using static Nuke.Common.Tools.AzureSignTool.AzureSignToolTasks;

class Build : NukeBuild
{
    const string LinuxRuntime = "linux-x64";
    const string WindowsRuntime = "win-x64";
    const string CiBranchNameEnvVariable = "CALAMARI_CurrentBranch";
    const string RootProjectName = "Calamari";
    static class Frameworks
    {
        public const string NetCoreApp31 = "netcoreapp3.1";
        public const string Net40 = "net40";
        public const string Net452 = "net452";
    }

    static class FixedRuntimes
    {
        public const string Cloud = "Cloud";
        public const string Portable = "portable";
    }

    readonly string[] TimestampUrls =
    {
        "http://timestamp.digicert.com?alg=sha256",
        "http://timestamp.comodoca.com"
    };

    [Parameter("Configuration to build - "
               + "Default is 'Debug' (local) or 'Release' (server)")] 
    readonly Configuration Configuration = 
        IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] [Required] readonly Solution? Solution;

    [Parameter("Whether to auto-detect the branch name - "
               + "this is okay for a local build, but should not be used under CI.")] 
    readonly bool AutoDetectBranch = IsLocalBuild;

    [Parameter("Branch name for Calamari to use to calculate the version number. "
               + "Can be set via the environment variable "
               + CiBranchNameEnvVariable
               + ".", Name = CiBranchNameEnvVariable)]
    readonly string? BranchName;

    [Parameter("Run packing step in parallel")] 
    readonly bool PackInParallel;

    [Parameter("Build verbosity")] 
    readonly string BuildVerbosity = "Minimal";

    [Parameter("Sign Binaries")] 
    readonly bool SignBinaries = false;

    [Parameter("Append Timestamp")] 
    readonly bool AppendTimestamp = false;

    [Parameter("Set Calamari Version on OctopusServer")] 
    readonly bool SetOctopusServerVersion = false;

    [Parameter("AzureKeyVaultUrl")] 
    readonly string? AzureKeyVaultUrl;
    
    [Parameter("AzureKeyVaultAppId")] 
    readonly string? AzureKeyVaultAppId;
    
    [Parameter("AzureKeyVaultAppSecret")] 
    readonly string? AzureKeyVaultAppSecret;
    
    [Parameter("AzureKeyVaultCertificateName")] 
    readonly string? AzureKeyVaultCertificateName;

    [Parameter("SigningCertificatePath")] 
    readonly string SigningCertificatePath = "./certificates/OctopusDevelopment.pfx";

    [Parameter("SigningCertificatePassword")] 
    readonly string SigningCertificatePassword = "Password01!";

    readonly string SolutionNullMessage =
        $"{nameof(Solution)} is null - this should be set by the Nuke {nameof(SolutionAttribute)}.";

    [Required] 
    [OctoVersion(BranchParameter = nameof(BranchName), AutoDetectBranchParameter = nameof(AutoDetectBranch))] 
    readonly OctoVersionInfo? OctoVersionInfo;

    static AbsolutePath SourceDirectory => RootDirectory / "source";

    static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    readonly AbsolutePath PublishDirectory = RootDirectory / "publish";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    readonly AbsolutePath LocalPackagesDir = RootDirectory / "../LocalPackages";

    Lazy<string> NugetVersion { get; } 

    public Build()
    {
        NugetVersion = new Lazy<string>(() => AppendTimestamp
                                            ? $"{OctoVersionInfo?.NuGetVersion}-{DateTime.Now:yyyyMMddHHmmss}"
                                            : OctoVersionInfo?.NuGetVersion 
                                              ?? throw new InvalidOperationException("Unable to retrieve valid Nuget Version"));
    }

    public static int Main() => Execute<Build>(x => x.BuildLocal);

    Target CheckForbiddenWords => _ => _.Executes(() =>
      {
          Console.WriteLine("Checking codebase for forbidden words.");

          var process = ProcessTasks.StartProcess(GitPath,
                                                  "grep -i -I -n -f ForbiddenWords.txt -- \"./*\" \":(exclude)ForbiddenWords.txt\"")
                                    .AssertWaitForExit();

          if (process.ExitCode == 1)
          {
              Console.WriteLine("Sanity check passed.");
              return;
          }

          var filesContainingForbiddenWords = process.Output.Select(o => o.Text).ToArray();
          if (filesContainingForbiddenWords.Any())
              throw new Exception("Found forbidden words in the following files, "
                                + "please clean them up:\r\n"
                                + string.Join("\r\n", filesContainingForbiddenWords));
      });

    Target Clean => _ => _.Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(PublishDirectory);
        });

    Target Restore => _ => _.DependsOn(Clean)
                            .Executes(() =>
          {
              var localRuntime = WindowsRuntime;

              if (!OperatingSystem.IsWindows())
              {
                  localRuntime = LinuxRuntime;
              }

              DotNetRestore(_ => _.SetProjectFile(Solution)
                                  .SetRuntime(localRuntime)
                                  .SetProperty("DisableImplicitNuGetFallbackFolder", true));
          });

    Target Compile => _ => _.DependsOn(CheckForbiddenWords)
                            .DependsOn(Restore)
                            .Executes(() =>
          {
              DotNetBuild(_ => _.SetProjectFile(Solution)
                                .SetConfiguration(Configuration)
                                .SetNoRestore(true)
                                .SetVersion(NugetVersion.Value)
                                .SetInformationalVersion(OctoVersionInfo?.InformationalVersion));
          });

    Target Publish => _ => _.DependsOn(Compile)
                            .Executes(() =>
          {
              if(!OperatingSystem.IsWindows())
                  Log.Warning("Building Calamari on a non-windows machine will result "
                            + "in the {DefaultNugetPackageName} and {CloudNugetPackageName} "
                            + "nuget packages being built as .Net Core 3.1 packages "
                            + "instead of as .Net Framework 4.0 and 4.5.2 respectively. "
                            + "This can cause compatibility issues when running certain "
                            + "deployment steps in Octopus Server", 
                              RootProjectName, $"{RootProjectName}.{FixedRuntimes.Cloud}");
              
              var nugetVersion = NugetVersion.Value;
              DoPublish(RootProjectName, 
                        OperatingSystem.IsWindows() ? 
                            Frameworks.Net40 : 
                            Frameworks.NetCoreApp31, 
                        nugetVersion);
              DoPublish(RootProjectName, 
                        OperatingSystem.IsWindows() ? 
                            Frameworks.Net452 : 
                            Frameworks.NetCoreApp31, 
                        nugetVersion, 
                        FixedRuntimes.Cloud);

              DoPublish(RootProjectName, Frameworks.NetCoreApp31, nugetVersion, FixedRuntimes.Portable);

              // Create the self-contained Calamari packages for each runtime ID defined in Calamari.csproj
              foreach (var rid in Solution?.GetProject(RootProjectName).GetRuntimeIdentifiers()!)
              {
                  DoPublish(RootProjectName, Frameworks.NetCoreApp31, nugetVersion, rid);
              }
          });

    Target PackBinaries => _ => _.DependsOn(Publish)
                                 .Executes(async () =>
           {
               var nugetVersion = NugetVersion.Value;
               var packageActions = new List<Action>
               {
                   () => DoPackage(RootProjectName, 
                                   OperatingSystem.IsWindows() ? 
                                       Frameworks.Net40 : 
                                       Frameworks.NetCoreApp31, 
                                   nugetVersion),
                   () => DoPackage(RootProjectName, 
                                   OperatingSystem.IsWindows() ? 
                                       Frameworks.Net452 : 
                                       Frameworks.NetCoreApp31, 
                                   nugetVersion, 
                                   FixedRuntimes.Cloud),
                   // Create a portable .NET Core package
                   () => DoPackage(RootProjectName, Frameworks.NetCoreApp31, nugetVersion, FixedRuntimes.Portable)
               };

               // Create the self-contained Calamari packages for each runtime ID defined in Calamari.csproj
               foreach(var rid in Solution?.GetProject(RootProjectName).GetRuntimeIdentifiers()!)
               {
                   packageActions.Add(() => DoPackage(RootProjectName, Frameworks.NetCoreApp31, nugetVersion, rid));
               }

               var dotNetCorePackSettings = new DotNetPackSettings().SetConfiguration(Configuration)
                                                                    .SetOutputDirectory(ArtifactsDirectory)
                                                                    .EnableNoBuild()
                                                                    .EnableIncludeSource()
                                                                    .SetVersion(nugetVersion)
                                                                    .SetNoRestore(true);

               var commonProjects = Directory.GetFiles(SourceDirectory, "*.Common.csproj", new EnumerationOptions{ RecurseSubdirectories = true});
               foreach(var project in commonProjects)
               {
                   packageActions.Add(() => SignAndPack(project.ToString(), dotNetCorePackSettings));
               }

               packageActions.Add(() => SignAndPack("./source/Calamari.CloudAccounts/Calamari.CloudAccounts.csproj", dotNetCorePackSettings));

               await RunPackActions(packageActions);
           });

    Target PackTests => _ => _.DependsOn(Compile)
                              .Executes(async () =>
            {
                var nugetVersion = NugetVersion.Value;
                var defaultTarget = OperatingSystem.IsWindows() ? "net461" : "netcoreapp3.1";
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
                foreach(var rid in Solution?.GetProject(@"Calamari.Tests").GetRuntimeIdentifiers()!)
                {
                    actions.Add(() => {
                                    var publishedLocation = DoPublish("Calamari.Tests", "netcoreapp3.1", nugetVersion, rid);
                                    var zipName = $"Calamari.Tests.netcoreapp.{rid}.{nugetVersion}.zip";
                                    CompressionTasks.Compress(publishedLocation, ArtifactsDirectory / zipName);
                                });
                }

                actions.Add(() =>
                            {
                                DotNetPack(new DotNetPackSettings().SetConfiguration(Configuration)
                                                                   .SetProject("./source/Calamari.Testing/Calamari.Testing.csproj")
                                                                   .SetOutputDirectory(ArtifactsDirectory)
                                                                   .EnableNoBuild()
                                                                   .EnableIncludeSource()
                                                                   .SetVersion(nugetVersion)
                                                                   .SetNoRestore(true));
                            });

                await RunPackActions(actions);
            });

    Target Pack => _ => _.DependsOn(PackBinaries)
                         .DependsOn(PackTests);

    Target CopyToLocalPackages => _ => _.Requires(() => IsLocalBuild)
                                        .DependsOn(PackBinaries)
                                        .Executes(() =>
          {
              Directory.CreateDirectory(LocalPackagesDir);
              foreach (var file in Directory.GetFiles(ArtifactsDirectory, "Calamari.*.nupkg"))
                  File.Copy(file, LocalPackagesDir / Path.GetFileName(file));
          });

    Target UpdateCalamariVersionOnOctopusServer => _ => _.Requires(() => SetOctopusServerVersion)
                                                         .Requires(() => IsLocalBuild)
                                                         .DependsOn(CopyToLocalPackages)
                                                         .Executes(() =>
           {
               var serverProjectFile = Path.GetFullPath("../OctopusDeploy/source/Octopus.Server/Octopus.Server.csproj");
               if (File.Exists(serverProjectFile))
               {
                   Console.WriteLine("Setting Calamari version in Octopus Server " + 
                                     "project {0} to {1}", 
                                     serverProjectFile, NugetVersion.Value);
                   
                   SetOctopusServerCalamariVersion(serverProjectFile);
               }
               else 
               {
                   Console.WriteLine("Could not set Calamari version in Octopus Server " + 
                                     "project {0} to {1} as could not find project file", 
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


    [UsedImplicitly]
    Target BuildCi => _ => _.DependsOn(SetTeamCityVersion)
                            .DependsOn(Pack)
                            .DependsOn(CopyToLocalPackages)
                            .DependsOn(UpdateCalamariVersionOnOctopusServer);

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
            {
                action();
            }
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

        SignAndTimestampBinaries(publishedTo);
        
        return publishedTo;
    }

    void SignAndPack(string project, DotNetPackSettings dotNetCorePackSettings){
        Console.WriteLine("SignAndPack project: " + project);
        
        var binDirectory = $"{Path.GetDirectoryName(project)}/bin/{Configuration}/";
        var binariesFolders = Directory.GetDirectories(binDirectory,"*", new EnumerationOptions{RecurseSubdirectories = true});
        foreach(var directory in binariesFolders){
            SignAndTimestampBinaries(directory);
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

        SignAndTimestampBinaries(publishedTo);

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

    void SignAndTimestampBinaries(string outputDirectory)
    {
        // When building locally signing isn't really necessary and it could take up to 3-4 minutes to sign all the binaries
        // as we build for many, many different runtimes so disabling it locally means quicker turn around when doing local development.
        if (IsLocalBuild && !SignBinaries) return;
    
        Console.WriteLine($"Signing binaries in {outputDirectory}");
    
        // check that any unsigned libraries, that Octopus Deploy authors, get signed to play nice with security scanning tools
        // refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
        // decision re: no signing everything: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1557938890227100
        var unsignedExecutablesAndLibraries = GetFilesFromDirectory(outputDirectory,
                                                                    "Calamari*.exe",
                                                                    "Calamari*.dll",
                                                                    "Octo*.exe",
                                                                    "Octo*.dll")
                                              .Where(f => !HasAuthenticodeSignature(f))
                                              .ToArray();

        if (AzureKeyVaultUrl.IsNullOrEmpty() && 
            AzureKeyVaultAppId.IsNullOrEmpty() && 
            AzureKeyVaultAppSecret.IsNullOrEmpty() && 
            AzureKeyVaultCertificateName.IsNullOrEmpty())
        {
            if (SigningCertificatePath.IsNullOrEmpty() || 
                SigningCertificatePassword.IsNullOrEmpty())
                throw new InvalidOperationException("Either Azure Key Vault or Signing Certificate Parameters must be set.");

            Console.WriteLine("Signing files using signtool and the self-signed development code signing certificate.");
            SignFilesWithSignTool(unsignedExecutablesAndLibraries, 
                                  SigningCertificatePath, 
                                  SigningCertificatePassword);
        }
        else
        {
            Console.WriteLine("Signing files using azuresigntool and the production code signing certificate");
            SignFilesWithAzureSignTool(unsignedExecutablesAndLibraries, 
                                       AzureKeyVaultUrl!, 
                                       AzureKeyVaultAppId!, 
                                       AzureKeyVaultAppSecret!,
                                       AzureKeyVaultCertificateName!);
        }
    
        TimeStampFiles(unsignedExecutablesAndLibraries);
    }

    static IEnumerable<string> GetFilesFromDirectory(string directory, params string[] searchPatterns)
    {
        return searchPatterns.SelectMany(searchPattern => Directory.GetFiles(directory, searchPattern));
    }

    // note: Doesn't check if existing signatures are valid, only that one exists
    // source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
    bool HasAuthenticodeSignature(string filePath)
    {
        try
        {
            X509Certificate.CreateFromSignedFile(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static void SignFilesWithAzureSignTool(ICollection<string> files,
                                    string vaultUrl,
                                    string vaultAppId,
                                    string vaultAppSecret,
                                    string vaultCertificateName,
                                    string display = "",
                                    string displayUrl = "")
    {
        Console.WriteLine($"Finished signing {files.Count} files.");

        AzureSignTool(_ => _.SetKeyVaultUrl(vaultUrl)
                            .SetKeyVaultClientId(vaultAppId)
                            .SetKeyVaultClientSecret(vaultAppSecret)
                            .SetKeyVaultCertificateName(vaultCertificateName)
                            .SetFileDigest("sha256")
                            .SetDescription(display)
                            .SetDescriptionUrl(displayUrl)
                            .SetFiles(files));
    }

    void SignFilesWithSignTool(IReadOnlyCollection<string> files,
                               string certificatePath,
                               string certificatePassword,
                               string display = "",
                               string displayUrl = "")
    {
        if (!File.Exists(certificatePath))
            throw new Exception($"The code-signing certificate was not found at {certificatePath}.");
    
        Console.WriteLine($"Signing {files.Count} files using certificate at '{certificatePath}'...");

        SignToolTasks.SignTool(_ => _.SetFileDigestAlgorithm(SignToolDigestAlgorithm.SHA256)
                                     .SetFile(certificatePath)
                                     .SetPassword(certificatePassword)
                                     .SetDescription(display)
                                     .SetUrl(displayUrl)
                                     .AddFiles(files));
        
        Console.WriteLine($"Finished signing {files.Count} files.");
    }

    void TimeStampFiles(ICollection<string> files)
    {
        Console.WriteLine($"Timestamping {files.Count} files...");
    
        var timestamped = false;
        foreach (var url in TimestampUrls)
        {
            try
            {
                var argumentsBuilder = new StringBuilder("timestamp")
                                       .Append("/tr")
                                       .Append($"\"{url}\"")
                                       .Append("/td")
                                       .Append("sha256");
    
                foreach (var file in files)
                    argumentsBuilder.Append($"\"{file}\"");
                
                var process = ProcessTasks.StartProcess(SignToolTasks.SignToolPath, argumentsBuilder.ToString())
                                          .AssertWaitForExit();
                
                if (process.ExitCode != 0)
                    throw new Exception($"Timestamping files failed with the exit code {process.ExitCode}. " + 
                                        "Look for 'SignTool Error' in the logs.");
                
                timestamped = true;
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine($"Failed to timestamp files using {url}. Maybe we can try another timestamp service...");
            }
        }
    
        if (!timestamped)
        {
            throw new Exception($"Failed to timestamp files even after we tried all of the timestamp services we use.");
        }
    
        Console.WriteLine($"Finished timestamping {files.Count} files.");
    }
    
    // Sets the Octopus.Server Calamari version property
    void SetOctopusServerCalamariVersion(string projectFile)
    {
        var text = File.ReadAllText(projectFile);
        text = Regex.Replace(text, @"<CalamariVersion>([\S])+<\/CalamariVersion>", $"<CalamariVersion>{NugetVersion.Value}</CalamariVersion>");
        File.WriteAllText(projectFile, text);
    }
}
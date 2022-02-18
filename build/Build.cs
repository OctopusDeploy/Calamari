using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.AzureSignTool;
using Nuke.Common.Tools.OctoVersion;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Tools.SignTool;
using Serilog.Sinks.File;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;
using static Nuke.Common.Tools.AzureSignTool.AzureSignToolTasks;

class Build : NukeBuild
{
    const string LinuxRuntime = "linux-x64";
    const string WindowsRuntime = "win-x64";
    const string CiBranchNameEnvVariable = "CALAMARI_CurrentBranch";
    
    readonly string[] TimestampUrls = new string[]
    {
        "http://timestamp.digicert.com?alg=sha256",
        "http://timestamp.comodoca.com"
    };

    [Parameter("Configuration to build - "
               + "Default is 'Debug' (local) or 'Release' (server)")] 
    readonly Configuration Configuration = 
        IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Test filter expression", Name = "where")] 
    readonly string TestFilter = string.Empty;

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
    readonly string BuildVerbosity = "normal";

    [Parameter("Sign Binaries")] 
    readonly bool SignBinaries = false;

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

    readonly string OctoVersionInfoNullMessage =
        $"{nameof(OctoVersionInfo)} is null - this should be set by the Nuke {nameof(OctoVersionAttribute)}." + $" Ensure the CI server has set the \"{CiBranchNameEnvVariable}\" environment variable.";

    readonly string SolutionNullMessage =
        $"{nameof(Solution)} is null - this should be set by the Nuke {nameof(SolutionAttribute)}.";

    [Required] [OctoVersion(BranchParameter = nameof(BranchName), AutoDetectBranchParameter = nameof(AutoDetectBranch))] readonly OctoVersionInfo? OctoVersionInfo;

    AbsolutePath SourceDirectory => RootDirectory / "source";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    
    AbsolutePath PublishDirectory = RootDirectory / "publish";

    AbsolutePath OutputDirectory => RootDirectory / "output";

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

                                          DotNetRestore(
                                                        s => s.SetProjectFile(Solution)
                                                              .SetRuntime(localRuntime)
                                                              .SetProperty("DisableImplicitNuGetFallbackFolder", true));
                                      });

    Target Compile => _ => _.DependsOn(CheckForbiddenWords)
                            .DependsOn(Restore)
                            .Executes(() =>
                                      {
                                          DotNetBuild(_ => _.SetProjectFile(Solution)
                                                            .SetConfiguration(Configuration)
                                                            .EnableNoRestore()
                                                            .SetVersion(OctoVersionInfo?.FullSemVer)
                                                            .SetInformationalVersion(OctoVersionInfo?.InformationalVersion));
                                      });

    Target PackBinaries => _ => _.DependsOn(Compile)
                                 .Executes(async () =>
                                           {
                                               var nugetVersion = OctoVersionInfo?.FullSemVer;

                                               if (nugetVersion is null)
                                                   throw new InvalidOperationException("Unable to get nugetVersion from OctoVersion.");
                                               
                                               var actions = new List<Action>();

                                               if (OperatingSystem.IsWindows())
                                               {
                                                   actions.Add(() => DoPackage("Calamari", "net40", nugetVersion));    
                                                   actions.Add(() => DoPackage("Calamari", "net452", nugetVersion, "Cloud"));
                                               }

                                               // Create a portable .NET Core package
                                               actions.Add(() => DoPackage("Calamari", "netcoreapp3.1", nugetVersion, "portable"));

                                               // Create the self-contained Calamari packages for each runtime ID defined in Calamari.csproj
                                               foreach(var rid in Solution?.GetProject("Calamari").GetRuntimeIdentifiers()!)
                                               {
                                                   actions.Add(() => DoPackage("Calamari", "netcoreapp3.1", nugetVersion, rid));
                                               }

                                               var dotNetCorePackSettings = new DotNetPackSettings().SetConfiguration(Configuration)
                                                                                                    .SetOutputDirectory(ArtifactsDirectory)
                                                                                                    .EnableNoBuild()
                                                                                                    .EnableIncludeSource()
                                                                                                    .SetVersion(nugetVersion);

                                               var commonProjects = Directory.GetFiles(SourceDirectory, "*.Common.csproj", new EnumerationOptions{ RecurseSubdirectories = true});
                                               foreach(var project in commonProjects)
                                               {
                                                   actions.Add(() => SignAndPack(project.ToString(), dotNetCorePackSettings));
                                               }
    
                                               actions.Add(() => SignAndPack("./source/Calamari.CloudAccounts/Calamari.CloudAccounts.csproj", dotNetCorePackSettings));
    
                                               await RunPackActions(actions);
                                           });

    Target PackTests => _ => _.DependsOn(Compile)
                              .Executes(() =>
                                        {
                                            Console.WriteLine("PackTests");
                                            //TODO:
                                        });

    Target Pack => _ => _.DependsOn(PackBinaries)
                         .DependsOn(PackTests);

    Target CopyToLocalPackages => _ => _
                                      //TODO: What's the Nuke equiv of .WithCriteria(BuildSystem.IsLocalBuild)
                                      .Executes(() =>
                                                {
                                                    Console.WriteLine("CopyToLocalPackages");
                                                    //TODO:
                                                });

    Target SetOctopusServerVersion => _ => _
                                          //TODO:     .WithCriteria(BuildSystem.IsLocalBuild)
                                          //TODO:     .WithCriteria(setOctopusServerVersion)
                                          .Executes(() =>
                                                    {
                                                        Console.WriteLine("SetOctopusServerVersion");
                                                        //TODO:
                                                    });

    Target SetTeamCityVersion => _ => _.Executes(() =>
                                                 {
                                                     Console.WriteLine("SetTeamCityVersion");
                                                     //TODO:
                                                 });

    Target BuildLocal => _ => _.DependsOn(PackBinaries)
                               .DependsOn(CopyToLocalPackages)
                               .DependsOn(SetOctopusServerVersion);


    Target BuildCI => _ => _.DependsOn(SetTeamCityVersion)
                            .DependsOn(Pack)
                            .DependsOn(CopyToLocalPackages)
                            .DependsOn(SetOctopusServerVersion);
    private async Task RunPackActions(List<Action> actions) 
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

    private AbsolutePath DoPublish(string project, string framework, string version, string? runtimeId = null) 
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
                            .SetVersion(OctoVersionInfo?.FullSemVer)
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
    private void DoPackage(string project, string framework, string version, string? runtimeId = null)
    {
        var projectDir = SourceDirectory / project;
        var packageId = $"{project}";
        var nugetPackProperties = new Dictionary<string, object>();

        var publishedTo = DoPublish(project, framework, version, runtimeId);

        if (!string.IsNullOrEmpty(runtimeId))
        {
            packageId = $"{project}.{runtimeId}";
            nugetPackProperties.Add("runtimeId", runtimeId);
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
                                   .SetVersion(OctoVersionInfo?.FullSemVer)
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
}
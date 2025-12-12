using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using JetBrains.Annotations;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.OctoVersion;
using Nuke.Common.Utilities.Collections;

namespace Calamari.Build;

partial class Build : NukeBuild
{
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")] readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Required] readonly Solution Solution = SolutionModelTasks.ParseSolution(KnownPaths.SourceDirectory / "Calamari.sln");

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

    string ConsolidatedPackagePath = "";

    public Build()
    {
        // Mimic the behaviour of this attribute, but lazily so we don't pay the OctoVersion cost when it isn't needed
        OctoVersionInfo = new Lazy<OctoVersionInfo?>(() =>
                                                     {
                                                         var attribute = new OctoVersionAttribute { BranchMember = nameof(BranchName), Framework = "net8.0"};

                                                         // the Attribute does all the work such as calling TeamCity.Instance?.SetBuildNumber for us
                                                         var version = attribute.GetValue(null!, this);

                                                         return version as OctoVersionInfo;
                                                     }, LazyThreadSafetyMode.ExecutionAndPublication);

        NugetVersion = new Lazy<string>(GetNugetVersion);

        // This initialisation is required to ensure the build script can
        // perform actions such as GetRuntimeIdentifiers() on projects.
        ProjectModelTasks.Initialize();
    }
    
    static AbsolutePath BuildDirectory => RootDirectory / "build";
    static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    static AbsolutePath PublishDirectory => RootDirectory / "publish";
    static AbsolutePath LocalPackagesDirectory => RootDirectory / ".." / "LocalPackages";


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
            d.DependsOn(CheckForbiddenWords)
             .Executes(() =>
                       {
                           KnownPaths.SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(d => d.DeleteDirectory());
                           
                           KnownPaths.ArtifactsDirectory.CreateOrCleanDirectory();
                           KnownPaths.PublishDirectory.CreateOrCleanDirectory();
                       });

    Target RestoreSolution =>
        d =>
            d.DependsOn(Clean)
             .Executes(() =>
                       {
                           //Do one big, default restore
                           DotNetRestore(s => s.SetProjectFile(Solution));

                           var allRuntimeIds = ListAllRuntimeIdentifiersInSolution();
                           //we restore for all individual runtimes
                           foreach (var runtimeId in allRuntimeIds)
                           {
                               DotNetRestore(s => s.SetProjectFile(Solution).SetRuntime(runtimeId));
                           }
                       });
    
    Target SetTeamCityVersion => d => d.Executes(() => TeamCity.Instance?.SetBuildNumber(NugetVersion.Value));

    Target BuildCi => d =>
                          d.DependsOn(PublishCalamariProjects)
                           .DependsOn(PackCalamariConsolidatedNugetPackage)
                           .DependsOn(PublishNukeBuild);
    public static int Main() => Execute<Build>(x => IsServerBuild ? x.BuildCi : x.BuildLocal);

    string GetNugetVersion()
    {
        return AppendTimestamp
            ? $"{OctoVersionInfo.Value?.NuGetVersion}-{DateTime.Now:yyyyMMddHHmmss}"
            : OctoVersionInfo.Value?.NuGetVersion
              ?? throw new InvalidOperationException("Unable to retrieve valid Nuget Version");
    }
}
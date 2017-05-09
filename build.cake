//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0-beta0011"

using Path = System.IO.Path;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var testFilter = Argument("where", "");
var signingCertificatePath = Argument("signing_certificate_path", "");
var signingCertificatePassword = Argument("signing_certificate_password", "");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var artifactsDir = "./built-packages/";
var localPackagesDir = "../LocalPackages";
var sourceFolder = "./source/";
GitVersion gitVersionInfo;
string nugetVersion;

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
	gitVersionInfo = GitVersion(new GitVersionSettings {
        OutputType = GitVersionOutput.Json
    });

    if(BuildSystem.IsRunningOnTeamCity) {
		BuildSystem.TeamCity.SetBuildNumber(gitVersionInfo.NuGetVersion);
	}
	
    nugetVersion = gitVersionInfo.NuGetVersion;
	
    Information("Building Calamari v{0}", nugetVersion);
});

Teardown(context =>
{
    Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
//  PRIVATE TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
    CleanDirectories("./**/bin");
    CleanDirectories("./**/obj");
});

Task("Restore")
	.IsDependentOn("Clean")
    .Does(() => DotNetCoreRestore("source"));

Task("Build")
    .IsDependentOn("Restore")
    .Does(() => {
		 DotNetCoreBuild("./source", new DotNetCoreBuildSettings
		{
			Configuration = configuration
		});
	});

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
		var projects = GetFiles("./source/**/*Tests.csproj");
		foreach(var project in projects)
			DotNetCoreTest(project.FullPath, new DotNetCoreTestSettings
			{
				Configuration = configuration,
				NoBuild = true,
				ArgumentCustomization = args => {
					if(!string.IsNullOrEmpty(testFilter)) {
						args = args.Append("--where").AppendQuoted(testFilter);
					}
					return args.Append("--logger:trx");
				}
			});
	});
	
Task("Publish")
	.IsDependentOn("Build")
    .WithCriteria(BuildSystem.IsRunningOnTeamCity)
    .Does(() =>
{
    var isPullRequest = !String.IsNullOrEmpty(EnvironmentVariable("APPVEYOR_PULL_REQUEST_NUMBER"));
    var isMasterBranch = EnvironmentVariable("APPVEYOR_REPO_BRANCH") == "master" && !isPullRequest;
    var shouldPushToMyGet = !BuildSystem.IsLocalBuild;
    var shouldPushToNuGet = !BuildSystem.IsLocalBuild && isMasterBranch;

    if (shouldPushToMyGet)
    {
        NuGetPush("artifacts/Calamari." + nugetVersion + ".nupkg", new NuGetPushSettings {
            Source = "https://octopus.myget.org/F/octopus-dependencies/api/v3/index.json",
            ApiKey = EnvironmentVariable("MyGetApiKey")
        });
        NuGetPush("artifacts/Calamari.Azure." + nugetVersion + ".nupkg", new NuGetPushSettings {
            Source = "https://octopus.myget.org/F/octopus-dependencies/api/v3/index.json",
            ApiKey = EnvironmentVariable("MyGetApiKey")
        });
    }
});

Task("CopyToLocalPackages")
	.IsDependentOn("Publish")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .Does(() =>
{
    CreateDirectory(localPackagesDir);
    CopyFileToDirectory(Path.Combine(artifactsDir, $"Calamari.{nugetVersion}.nupkg"), localPackagesDir);
    CopyFileToDirectory(Path.Combine(artifactsDir, $"Calamari.Azure.{nugetVersion}.nupkg"), localPackagesDir);
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .IsDependentOn("BuildPackAndZipTestBinaries");

Task("BuildPackAndZipTestBinaries")
    .IsDependentOn("CopyToLocalPackages");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);

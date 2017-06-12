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
var localPackagesDir = "../LocalPackages";
var sourceFolder = "./source/";
var artifactsDir = "./artifacts";
var publishDir = "./publish";

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

Task("SetTeamCityVersion")
    .Does(() => {
        if(BuildSystem.IsRunningOnTeamCity)
            BuildSystem.TeamCity.SetBuildNumber(gitVersionInfo.NuGetVersion);
    });

Task("Clean")
	.IsDependentOn("SetTeamCityVersion")
    .Does(() =>
{
    CleanDirectories(publishDir);
    CleanDirectories(artifactsDir);
    CleanDirectories("./**/bin");
    CleanDirectories("./**/obj");
});

Task("Restore")
	.IsDependentOn("Clean")
    .Does(() => DotNetCoreRestore("source", new DotNetCoreRestoreSettings
    {
	    ArgumentCustomization = args => args.Append($"--verbosity normal")
    }));

Task("Build")
    .IsDependentOn("Restore")
    .Does(() => 
	{
		DotNetCoreBuild("./source/Calamari.sln", new DotNetCoreBuildSettings
		{
			Configuration = configuration,
			ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}").Append($"--verbosity normal")
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
					return args.Append("--logger:trx")
                        .Append($"--verbosity normal");
				}
			});
	});
	
Task("Pack")
	.IsDependentOn("Build")
    .Does(() =>
{
    DoPackage("Calamari", "net40", nugetVersion);
    DoPackage("Calamari.Azure", "net451", nugetVersion);   
    Zip("./source/Calamari.Tests/bin/Release/net452/", Path.Combine(artifactsDir, "Binaries.zip"));
});

Task("CopyToLocalPackages")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .IsDependentOn("Pack")
    .Does(() =>

{
    CreateDirectory(localPackagesDir);
    CopyFileToDirectory(Path.Combine(artifactsDir, $"Calamari.{nugetVersion}.nupkg"), localPackagesDir);
    CopyFileToDirectory(Path.Combine(artifactsDir, $"Calamari.Azure.{nugetVersion}.nupkg"), localPackagesDir);
});

private void DoPackage(string project, string framework, string version)
{ 
	var publishedTo = Path.Combine(publishDir, project, framework);
	var projectDir = Path.Combine("./source", project);

    DotNetCorePublish(projectDir, new DotNetCorePublishSettings
    {
        Configuration = configuration,
        OutputDirectory = publishedTo,
        Framework = framework,
		ArgumentCustomization = args => args.Append($"--verbosity normal")
    });

	var nuspec = $"{publishedTo}/{project}.nuspec";
	CopyFile($"{projectDir}/{project}.nuspec", nuspec);

    NuGetPack(nuspec, new NuGetPackSettings
    {
        OutputDirectory = artifactsDir,
		BasePath = publishedTo,
		Version = nugetVersion,
		Verbosity = NuGetVerbosity.Normal
    });
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .IsDependentOn("SetTeamCityVersion")
    .IsDependentOn("CopyToLocalPackages");

	
//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);

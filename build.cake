//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0-beta0011"

using Path = System.IO.Path;
using System.Xml;

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
        OutputType = GitVersionOutput.Json,
		LogFilePath = "gitversion.log"
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
	DoPackage("Calamari.Cloud", "net451", nugetVersion);
    Zip("./source/Calamari.Tests/bin/Release/net452/", Path.Combine(artifactsDir, "Binaries.zip"));

    // Create a portable .NET Core package
    DoPackage("Calamari", "netcoreapp2.0", nugetVersion, "portable");

    // Create the self-contained Calamari packages for each runtime ID defined in Calamari.csproj
    foreach(var rid in GetProjectRuntimeIds(@".\source\Calamari\Calamari.csproj"))
    {
        DoPackage("Calamari", "netcoreapp2.0", nugetVersion, rid);
    }
});

Task("CopyToLocalPackages")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .IsDependentOn("Pack")
    .Does(() =>

{
    CreateDirectory(localPackagesDir);
    CopyFiles(Path.Combine(artifactsDir, $"Calamari.*.nupkg"), localPackagesDir);
});

private void DoPackage(string project, string framework, string version, string runtimeId = null)
{
    var publishedTo = Path.Combine(publishDir, project, framework);
    var projectDir = Path.Combine("./source", project);
    var packageId = $"{project}";
    var nugetPackProperties = new Dictionary<string,string>();
    var publishSettings = new DotNetCorePublishSettings
    {
        Configuration = configuration,
        OutputDirectory = publishedTo,
        Framework = framework,
		ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}").Append($"--verbosity normal")
    };
    if (!string.IsNullOrEmpty(runtimeId))
    {
        publishedTo = Path.Combine(publishedTo, runtimeId);
        publishSettings.OutputDirectory = publishedTo;
        // "portable" is not an actual runtime ID. We're using it to represent the portable .NET core build.
        publishSettings.Runtime = (runtimeId != null && runtimeId != "portable") ? runtimeId : null;
        packageId = $"{project}.{runtimeId}";
        nugetPackProperties.Add("runtimeId", runtimeId);
    }
    var nugetPackSettings = new NuGetPackSettings
    {
        Id = packageId,
        OutputDirectory = artifactsDir,
		BasePath = publishedTo,
		Version = nugetVersion,
		Verbosity = NuGetVerbosity.Normal,
        Properties = nugetPackProperties
    };

    DotNetCorePublish(projectDir, publishSettings);

    SignBinaries(publishSettings.OutputDirectory.FullPath);

    var nuspec = $"{publishedTo}/{packageId}.nuspec";
    CopyFile($"{projectDir}/{project}.nuspec", nuspec);
    NuGetPack(nuspec, nugetPackSettings);
}

private void SignBinaries(string outputDirectory)
{
    Information($"Signing binaries in {outputDirectory}");

	var files = GetFiles(outputDirectory + "/Calamari.exe");
	files.Add(GetFiles(outputDirectory + "/Calamari.Cloud.exe"));
    files.Add(GetFiles(outputDirectory + "/Calamari.dll"));


    var signTool = MakeAbsolute(File("./certificates/signtool.exe"));
    Information($"Using signtool in {signTool}");

	Sign(files, new SignToolSignSettings {
			ToolPath = signTool,
            TimeStampUri = new Uri("http://timestamp.globalsign.com/scripts/timestamp.dll"),
            CertPath = signingCertificatePath,
            Password = signingCertificatePassword
    });

}

// Returns the runtime identifiers from the project file
private IEnumerable<string> GetProjectRuntimeIds(string projectFile)
{
    var doc = new XmlDocument();
    doc.Load(projectFile);
    var rids = doc.SelectSingleNode("Project/PropertyGroup/RuntimeIdentifiers").InnerText;
    return rids.Split(';');
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

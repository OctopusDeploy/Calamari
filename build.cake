//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0-beta0012"

using Path = System.IO.Path;
using System.Xml;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
	DoPackage("Calamari", "net452", nugetVersion, "Cloud");
    Zip("./source/Calamari.Tests/bin/Release/net452/", Path.Combine(artifactsDir, "Binaries.zip"));

    // Create a portable .NET Core package
    DoPackage("Calamari", "netcoreapp2.0", nugetVersion, "portable");

    // Create the self-contained Calamari packages for each runtime ID defined in Calamari.csproj
    foreach(var rid in GetProjectRuntimeIds(@".\source\Calamari\Calamari.csproj"))
    {
        DoPackage("Calamari", "netcoreapp2.0", nugetVersion, rid);
    }
	
	// Create a Zip for each runtime for testing
	foreach(var rid in GetProjectRuntimeIds(@".\source\Calamari.Tests\Calamari.Tests.csproj"))
    {
		var publishedLocation = DoPublish("Calamari.Tests", "netcoreapp2.0", nugetVersion, rid);
		var zipName = $"Calamari.Tests.netcoreapp2.{rid}.{nugetVersion}.zip";
		Zip(Path.Combine(publishedLocation, rid), Path.Combine(artifactsDir, zipName));
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

private string DoPublish(string project, string framework, string version, string runtimeId = null) {
	var projectDir = Path.Combine("./source", project);
	var publishedTo = Path.Combine(publishDir, project, framework);
   
   var publishSettings = new DotNetCorePublishSettings
    {
        Configuration = configuration,
        OutputDirectory = publishedTo,
        Framework = framework,
		ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}").Append($"--verbosity normal")
    };
	
	 if (!string.IsNullOrEmpty(runtimeId))
    {
        publishSettings.OutputDirectory = Path.Combine(publishedTo, runtimeId);
        // "portable" is not an actual runtime ID. We're using it to represent the portable .NET core build.
        publishSettings.Runtime = (runtimeId != null && runtimeId != "portable") ? runtimeId : null;
    }
	DotNetCorePublish(projectDir, publishSettings);

	SignBinaries(publishSettings.OutputDirectory.FullPath);
	return publishedTo;
}


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

    // check that any unsigned executables or libraries get signed, to play nice with security scanning tools
    // refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
    
     var unsignedExecutablesAndLibraries = 
         GetFiles(outputDirectory + "/*.exe")
         .Union(GetFiles(outputDirectory + "/*.dll"))
         .Where(f => !HasAuthenticodeSignature(f));

    var signTool = MakeAbsolute(File("./certificates/signtool.exe"));
    Information($"Using signtool in {signTool}");

	Sign(unsignedExecutablesAndLibraries, new SignToolSignSettings {
			ToolPath = signTool,
            TimeStampUri = new Uri("http://timestamp.globalsign.com/scripts/timestamp.dll"),
            CertPath = signingCertificatePath,
            Password = signingCertificatePassword
    });

}
// note: Doesn't check if existing signatures are valid, only that one exists 
// source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
private bool HasAuthenticodeSignature(FilePath fileInfo)
{
    try
    {
        X509Certificate.CreateFromSignedFile(fileInfo.FullPath);
        return true;
    } catch
    {
        return false;
    }
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

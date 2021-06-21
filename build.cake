//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=5.2.0"
#addin "nuget:?package=Cake.Incubator&version=5.0.1"
#addin "nuget:?package=Cake.FileHelpers&version=4.0.1"

using Path = System.IO.Path;
using System.Xml;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Task = System.Threading.Tasks.Task;
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var testFilter = Argument("where", "");
var signingCertificatePath = Argument("signing_certificate_path", "");
var signingCertificatePassword = Argument("signing_certificate_password", "");
var buildVerbosity = Argument("build_verbosity", "normal");
var packInParallel = Argument<bool>("packinparallel", false);
var appendTimestamp = Argument<bool>("timestamp", false);
var setOctopusServerVersion = Argument<bool>("setoctopusserverversion", false);

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var localPackagesDir = "../LocalPackages";
var sourceFolder = "./source/";
var artifactsDir = "./artifacts";
var publishDir = "./publish";
var signToolPath = MakeAbsolute(File("./certificates/signtool.exe"));
GitVersion gitVersionInfo;
string nugetVersion;

// From time to time the timestamping services go offline, let's try a few of them so our builds are more resilient
var timestampUrls = new string[]
{
    "http://timestamp.digicert.com?alg=sha256",
    "http://timestamp.comodoca.com"
};

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

    if (appendTimestamp) 
        nugetVersion = nugetVersion + "-" + DateTime.Now.ToString("yyyyMMddHHmmss");

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
            BuildSystem.TeamCity.SetBuildNumber(nugetVersion);
    });

Task("CheckForbiddenWords")
	.Does(() =>
{
	Information("Checking codebase for forbidden words.");

	IEnumerable<string> redirectedOutput;
 	var exitCodeWithArgument =
    	StartProcess(
        	"git",
        	new ProcessSettings {
            	Arguments = "grep -i -I -n -f ForbiddenWords.txt -- \"./*\" \":!ForbiddenWords.txt\"",
             	RedirectStandardOutput = true
        	},
        	out redirectedOutput
     	);

	var filesContainingForbiddenWords = redirectedOutput.ToArray();
	if (filesContainingForbiddenWords.Any())
		throw new Exception("Found forbidden words in the following files, please clean them up:\r\n" + string.Join("\r\n", filesContainingForbiddenWords));

	Information("Sanity check passed.");
});

Task("Clean")
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
	    ArgumentCustomization = args => args.Append("--verbosity").Append(buildVerbosity)
    }));

Task("Build")
    .IsDependentOn("CheckForbiddenWords")
    .IsDependentOn("Restore")
    .Does(() =>
	{
		DotNetCoreBuild("./source/Calamari.sln", new DotNetCoreBuildSettings
		{
			Configuration = configuration,
			ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}").Append("--verbosity").Append(buildVerbosity)
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
                        .Append("--verbosity").Append(buildVerbosity);
				}
			});
	});

Task("PackBinaries")
	.IsDependentOn("Build")
    .Does(async () =>
{
    var actions = new List<Action>();

    actions.Add(() => DoPackage("Calamari", "net40", nugetVersion));
    actions.Add(() => DoPackage("Calamari", "net452", nugetVersion, "Cloud"));

    // Create a portable .NET Core package
    actions.Add(() => DoPackage("Calamari", "netcoreapp3.1", nugetVersion, "portable"));

    // Create the self-contained Calamari packages for each runtime ID defined in Calamari.csproj
    foreach(var rid in GetProjectRuntimeIds(@".\source\Calamari\Calamari.csproj"))
    {
        actions.Add(() => DoPackage("Calamari", "netcoreapp3.1", nugetVersion, rid));
    }

    var dotNetCorePackSettings = GetDotNetCorePackSettings();

    var commonProjects = GetFiles("./source/**/*.Common.csproj");
    foreach(var project in commonProjects)
    {
        actions.Add(() => DotNetCorePack(project.ToString(), dotNetCorePackSettings));
    }

    actions.Add(() => DotNetCorePack("./source/Calamari.CloudAccounts/Calamari.CloudAccounts.csproj", dotNetCorePackSettings));

    await RunPackActions(actions);
});

Task("PackTests")
	.IsDependentOn("Build")
    .Does(async () =>
{
    var actions = new List<Action>();

    actions.Add(() => Zip("./source/Calamari.Tests/bin/Release/net452/", Path.Combine(artifactsDir, "Binaries.zip")));

    // Create a Zip for each runtime for testing
	foreach(var rid in GetProjectRuntimeIds(@".\source\Calamari.Tests\Calamari.Tests.csproj"))
    {
        actions.Add(() => {
            var publishedLocation = DoPublish("Calamari.Tests", "netcoreapp3.1", nugetVersion, rid);
            var zipName = $"Calamari.Tests.netcoreapp.{rid}.{nugetVersion}.zip";
            Zip(Path.Combine(publishedLocation, rid), Path.Combine(artifactsDir, zipName));
        });
    }

    var dotNetCorePackSettings = GetDotNetCorePackSettings();
    
    actions.Add(() => DotNetCorePack("./source/Calamari.Testing/Calamari.Testing.csproj", dotNetCorePackSettings));

    await RunPackActions(actions);
});

Task("Pack")
	.IsDependentOn("PackBinaries")
    .IsDependentOn("PackTests");

Task("CopyToLocalPackages")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .Does(() =>
{
    CreateDirectory(localPackagesDir);
    CopyFiles(Path.Combine(artifactsDir, $"Calamari.*.nupkg"), localPackagesDir);
});

Task("SetOctopusServerVersion")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .WithCriteria(setOctopusServerVersion)
    .Does(() =>
{
    var serverProjectFile = Path.GetFullPath("../OctopusDeploy/source/Octopus.Server/Octopus.Server.csproj");
    if (FileExists(serverProjectFile))
    {
        Information("Setting Calamari version in Octopus Server project {0} to {1}", serverProjectFile, nugetVersion);
        SetOctopusServerCalamariVersion(serverProjectFile);
    }
    else 
    {
        Information("Could not set Calamari version in Octopus Server project {0} to {1} as could not find project file", serverProjectFile, nugetVersion);
    }
});

private async Task RunPackActions(List<Action> actions) 
{
    if (packInParallel)
    {
        var tasks = new List<Task>();
        foreach (var action in actions)
        {
            tasks.Add(Task.Run(action));
        }
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

private string DoPublish(string project, string framework, string version, string runtimeId = null) {
	var projectDir = Path.Combine("./source", project);
	var publishedTo = Path.Combine(publishDir, project, framework);

   var publishSettings = new DotNetCorePublishSettings
    {
        Configuration = configuration,
        OutputDirectory = publishedTo,
        Framework = framework,
		ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}").Append("--verbosity").Append(buildVerbosity)
    };

	 if (!string.IsNullOrEmpty(runtimeId))
    {
        publishSettings.OutputDirectory = Path.Combine(publishedTo, runtimeId);
        // "portable" is not an actual runtime ID. We're using it to represent the portable .NET core build.
        publishSettings.Runtime = (runtimeId != null && runtimeId != "portable" && runtimeId != "Cloud") ? runtimeId : null;
    }
	DotNetCorePublish(projectDir, publishSettings);

	SignAndTimestampBinaries(publishSettings.OutputDirectory.FullPath);
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
		ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}").Append($"--verbosity").Append(buildVerbosity)
    };
    if (!string.IsNullOrEmpty(runtimeId))
    {
        publishedTo = Path.Combine(publishedTo, runtimeId);
        publishSettings.OutputDirectory = publishedTo;
        // "portable" is not an actual runtime ID. We're using it to represent the portable .NET core build.
        publishSettings.Runtime = (runtimeId != null && runtimeId != "portable" && runtimeId != "Cloud") ? runtimeId : null;
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
        Properties = nugetPackProperties,
        Symbols = true
    };

    DotNetCorePublish(projectDir, publishSettings);

    SignAndTimestampBinaries(publishSettings.OutputDirectory.FullPath);

    var nuspec = $"{publishedTo}/{packageId}.nuspec";
    CopyFile($"{projectDir}/{project}.nuspec", nuspec);
    NuGetPack(nuspec, nugetPackSettings);
}

private void SignAndTimestampBinaries(string outputDirectory)
{
    // When building locally signing isn't really necessary and it could take up to 3-4 minutes to sign all the binaries
    // as we build for many, many different runtimes so disabling it locally means quicker turn around when doing local development.
    if (BuildSystem.IsLocalBuild) return;

    Information($"Signing binaries in {outputDirectory}");

    // check that any unsigned libraries, that Octopus Deploy authors, get signed to play nice with security scanning tools
    // refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
    // decision re: no signing everything: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1557938890227100
     var unsignedExecutablesAndLibraries =
         GetFiles(
            outputDirectory + "/Calamari*.exe",
            outputDirectory + "/Calamari*.dll",
            outputDirectory + "/Octo*.exe",
            outputDirectory + "/Octo*.dll")
         .Where(f => !HasAuthenticodeSignature(f))
         .ToArray();

    Information($"Using signtool in {signToolPath}");
    SignFiles(unsignedExecutablesAndLibraries, signingCertificatePath, signingCertificatePassword);
    TimeStampFiles(unsignedExecutablesAndLibraries);
}
// note: Doesn't check if existing signatures are valid, only that one exists
// source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
private bool HasAuthenticodeSignature(FilePath filePath)
{
    try
    {
        X509Certificate.CreateFromSignedFile(filePath.FullPath);
        return true;
    }
    catch
    {
        return false;
    }
}

void SignFiles(IEnumerable<FilePath> files, FilePath certificatePath, string certificatePassword, string display = "", string displayUrl = "")
{
    if (!FileExists(signToolPath))
    {
        throw new Exception($"The signing tool was expected to be at the path '{signToolPath}' but wasn't available.");
    }

    if (!FileExists(certificatePath))
        throw new Exception($"The code-signing certificate was not found at {certificatePath}.");

    Information($"Signing {files.Count()} files using certificate at '{certificatePath}'...");

    var signArguments = new ProcessArgumentBuilder()
        .Append("sign")
        .Append("/fd SHA256")
        .Append("/f").AppendQuoted(certificatePath.FullPath)
        .Append($"/p").AppendQuotedSecret(certificatePassword);

    if (!string.IsNullOrWhiteSpace(display))
    {
        signArguments
            .Append("/d").AppendQuoted(display)
            .Append("/du").AppendQuoted(displayUrl);
    }

    foreach (var file in files)
    {
        signArguments.AppendQuoted(file.FullPath);
    }

    Information($"Executing: {signToolPath} {signArguments.RenderSafe()}");
    var exitCode = StartProcess(signToolPath, new ProcessSettings
    {
        Arguments = signArguments
    });

    if (exitCode != 0)
    {
        throw new Exception($"Signing files failed with the exit code {exitCode}. Look for 'SignTool Error' in the logs.");
    }

    Information($"Finished signing {files.Count()} files.");
}

private void TimeStampFiles(IEnumerable<FilePath> files)
{
    if (!FileExists(signToolPath))
    {
        throw new Exception($"The signing tool was expected to be at the path '{signToolPath}' but wasn't available.");
    }

    Information($"Timestamping {files.Count()} files...");

    var timestamped = false;
    foreach (var url in timestampUrls)
    {
        var timestampArguments = new ProcessArgumentBuilder()
            .Append($"timestamp")
            .Append("/tr").AppendQuoted(url)
            .Append("/td").Append("sha256");
            
        foreach (var file in files)
        {
            timestampArguments.AppendQuoted(file.FullPath);
        }

        try
        {
            Information($"Executing: {signToolPath} {timestampArguments.RenderSafe()}");
            var exitCode = StartProcess(signToolPath, new ProcessSettings
            {
                Arguments = timestampArguments
            });

            if (exitCode == 0)
            {
                timestamped = true;
                break;
            }
            else
            {
                throw new Exception($"Timestamping files failed with the exit code {exitCode}. Look for 'SignTool Error' in the logs.");
            }
        }
        catch (Exception ex)
        {
            Warning(ex.Message);
            Warning($"Failed to timestamp files using {url}. Maybe we can try another timestamp service...");
        }
    }

    if (!timestamped)
    {
        throw new Exception($"Failed to timestamp files even after we tried all of the timestamp services we use.");
    }

    Information($"Finished timestamping {files.Count()} files.");
}

// Returns the runtime identifiers from the project file
private IEnumerable<string> GetProjectRuntimeIds(string projectFile)
{
    var doc = new XmlDocument();
    doc.Load(projectFile);
    var rids = doc.SelectSingleNode("Project/PropertyGroup/RuntimeIdentifiers").InnerText;
    return rids.Split(';');
}

// Sets the Octopus.Server Calamari version property
private void SetOctopusServerCalamariVersion(string projectFile)
{
    ReplaceRegexInFiles(projectFile, @"<CalamariVersion>([\S])+<\/CalamariVersion>", $"<CalamariVersion>{nugetVersion}</CalamariVersion>");
}

private DotNetCorePackSettings GetDotNetCorePackSettings()
{
    return new DotNetCorePackSettings
    {
        Configuration = configuration,
        OutputDirectory = artifactsDir,
        NoBuild = true,
        IncludeSource = true,
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    };
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .IsDependentOn("SetTeamCityVersion")
    .IsDependentOn("Pack")
    .IsDependentOn("CopyToLocalPackages")
    .IsDependentOn("SetOctopusServerVersion");

Task("Local")
    .IsDependentOn("PackBinaries")
    .IsDependentOn("CopyToLocalPackages")
    .IsDependentOn("SetOctopusServerVersion");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);

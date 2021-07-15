//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=5.2.0"
#addin "nuget:?package=Cake.Incubator&version=5.0.1"
#addin "nuget:?package=Cake.FileHelpers&version=4.0.1"
// see https://www.gep13.co.uk/blog/introducing-cake.dotnettool.module
#module nuget:?package=Cake.DotNetTool.Module&version=0.1.0
#tool "dotnet:?package=AzureSignTool&version=2.0.17"

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
// We sign all of our own assemblies - these are the arguments required to sign code using Azure Key Vault
// If these arguments are null then the signing defaults to using the local certificate and SignTool
var keyVaultUrl = Argument("AzureKeyVaultUrl", "");
var keyVaultAppId = Argument("AzureKeyVaultAppId", "");
var keyVaultAppSecret = Argument("AzureKeyVaultAppSecret", "");
var keyVaultCertificateName = Argument("AzureKeyVaultCertificateName", "");
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
    .IsDependentOn("CheckForbiddenWords")
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
    DoPackage("Calamari", "netcoreapp3.1", nugetVersion, "portable");

    // Create the self-contained Calamari packages for each runtime ID defined in Calamari.csproj
    foreach(var rid in GetProjectRuntimeIds(@".\source\Calamari\Calamari.csproj"))
    {
        DoPackage("Calamari", "netcoreapp3.1", nugetVersion, rid);
    }

	// Create a Zip for each runtime for testing
	foreach(var rid in GetProjectRuntimeIds(@".\source\Calamari.Tests\Calamari.Tests.csproj"))
    {
		var publishedLocation = DoPublish("Calamari.Tests", "netcoreapp3.1", nugetVersion, rid);
		var zipName = $"Calamari.Tests.netcoreapp.{rid}.{nugetVersion}.zip";
		Zip(Path.Combine(publishedLocation, rid), Path.Combine(artifactsDir, zipName));
    }

    var dotNetCorePackSettings = new DotNetCorePackSettings
    {
        Configuration = configuration,
        OutputDirectory = artifactsDir,
        NoBuild = true,
        IncludeSource = true,
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    };
    var commonProjects = GetFiles("./source/**/*.Common.csproj");
    foreach(var project in commonProjects)
    {
        DotNetCorePack(project.ToString(), dotNetCorePackSettings);
    }
    DotNetCorePack("./source/Calamari.CloudAccounts/Calamari.CloudAccounts.csproj", dotNetCorePackSettings);
    DotNetCorePack("./source/Calamari.Testing/Calamari.Testing.csproj", dotNetCorePackSettings);

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
		ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}").Append($"--verbosity normal")
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
        Properties = nugetPackProperties
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

    if (String.IsNullOrEmpty(keyVaultUrl) && String.IsNullOrEmpty(keyVaultAppId) && String.IsNullOrEmpty(keyVaultAppSecret) && String.IsNullOrEmpty(keyVaultCertificateName))
    {
      Information("Signing files using signtool and the self-signed development code signing certificate.");
      SignFilesWithSignTool(unsignedExecutablesAndLibraries, signingCertificatePath, signingCertificatePassword);
    }
    else
    {
      Information("Signing files using azuresigntool and the production code signing certificate");
      SignFilesWithAzureSignTool(unsignedExecutablesAndLibraries, keyVaultUrl, keyVaultAppId, keyVaultAppSecret, keyVaultCertificateName);
    }
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

void SignFilesWithAzureSignTool(IEnumerable<FilePath> files, string vaultUrl, string vaultAppId, string vaultAppSecret, string vaultCertificateName, string display = "", string displayUrl = "")
{
  var signArguments = new ProcessArgumentBuilder()
    .Append("sign")
    .Append("--azure-key-vault-url").AppendQuoted(vaultUrl)
    .Append("--azure-key-vault-client-id").AppendQuoted(vaultAppId)
    .Append("--azure-key-vault-client-secret").AppendQuotedSecret(vaultAppSecret)
    .Append("--azure-key-vault-certificate").AppendQuoted(vaultCertificateName)
    .Append("--file-digest sha256");

  if (!string.IsNullOrWhiteSpace(display))
  {
    signArguments
      .Append("--description").AppendQuoted(display)
      .Append("--description-url").AppendQuoted(displayUrl);
  }

  foreach (var file in files)
    signArguments.AppendQuoted(file.FullPath);

    var azureSignToolPath = MakeAbsolute(File("./tools/azuresigntool.exe"));

    if (!FileExists(azureSignToolPath))
        throw new Exception($"The azure signing tool was expected to be at the path '{azureSignToolPath}' but wasn't available.");

  Information($"Executing: {azureSignToolPath} {signArguments.RenderSafe()}");
  var exitCode = StartProcess(azureSignToolPath.FullPath, signArguments.Render());
    if (exitCode != 0)
        throw new Exception($"AzureSignTool failed with the exit code {exitCode}.");

  Information($"Finished signing {files.Count()} files.");
}

void SignFilesWithSignTool(IEnumerable<FilePath> files, FilePath certificatePath, string certificatePassword, string display = "", string displayUrl = "")
{
    if (!FileExists(signToolPath))
        throw new Exception($"The signing tool was expected to be at the path '{signToolPath}' but wasn't available.");

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

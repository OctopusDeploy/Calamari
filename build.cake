//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#module nuget:?package=Cake.DotNetTool.Module&version=0.4.0
#tool "dotnet:?package=GitVersion.Tool&version=5.3.5"
#tool "dotnet:?package=AzureSignTool&version=2.0.17"
#addin "Cake.FileHelpers&version=3.2.0"
#addin "nuget:?package=Cake.Incubator&version=5.0.1"
#addin "nuget:?package=Cake.FileHelpers&version=4.0.1"

using Path = System.IO.Path;
using IO = System.IO;
using Cake.Common.Xml;
using System.Security.Cryptography.X509Certificates;
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var signFiles = Argument<bool>("sign_files", false);
var signToolPath = MakeAbsolute(File("./certificates/signtool.exe"));
var keyVaultUrl = Argument("AzureKeyVaultUrl", "");
var keyVaultAppId = Argument("AzureKeyVaultAppId", "");
var keyVaultAppSecret = Argument("AzureKeyVaultAppSecret", "");
var keyVaultCertificateName = Argument("AzureKeyVaultCertificateName", "");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var publishDir = "./publish";
var artifactsDir = "./artifacts/";
var localPackagesDir = "../LocalPackages";

GitVersion gitVersionInfo;
string nugetVersion;

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
        OutputType = GitVersionOutput.Json
    });

    if(BuildSystem.IsRunningOnTeamCity)
        BuildSystem.TeamCity.SetBuildNumber(gitVersionInfo.NuGetVersion);

    nugetVersion = gitVersionInfo.NuGetVersion;

    Information("Building Sashimi v{0}", nugetVersion);
    Information("Informational Version {0}", gitVersionInfo.InformationalVersion);
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
    CleanDirectory(publishDir);
    CleanDirectory(artifactsDir);
    CleanDirectories("./source/**/bin");
    CleanDirectories("./source/**/obj");
    CleanDirectories("./source/**/TestResults");
});

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() => {
        DotNetCoreRestore("source");
    });


Task("Build")
    .IsDependentOn("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetCoreBuild("./source", new DotNetCoreBuildSettings
    {
        Configuration = configuration,
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    });
});

Task("Test")
    .IsDependentOn("Build")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .Does(() => {
		var projects = GetFiles("./source/**/*Tests.csproj");

        Parallel.ForEach(projects, project => {
            DotNetCoreTest(project.FullPath, new DotNetCoreTestSettings
			{
				Configuration = configuration,
				NoBuild = true
			});
        });
    });

Task("PublishCalamariProjects")
    .IsDependentOn("Build")
    .Does(() => {
        var projects = GetFiles("./source/**/Calamari*.csproj"); //We need Calamari & Calamari.Tests
		foreach(var project in projects)
        {
            var calamariFlavour = XmlPeek(project, "Project/PropertyGroup/AssemblyName");

            var frameworks = XmlPeek(project, "Project/PropertyGroup/TargetFrameworks") ??
                XmlPeek(project, "Project/PropertyGroup/TargetFramework");

            foreach(var framework in frameworks.Split(';'))
            {
                void RunPublish(string runtime, string platform) {
                    var publishSettings = new DotNetCorePublishSettings
                		    	    {
                		    	    	Configuration = configuration,
                                        OutputDirectory = $"{publishDir}/{calamariFlavour}/{platform}",
                                        Framework = framework,
                                        Runtime = runtime
                		    	    };

                    DotNetCorePublish(project.FullPath, publishSettings);
                
                    SignAndTimestampBinaries(publishSettings.OutputDirectory.FullPath);

                    CopyFiles("./global.json", $"{publishDir}/{calamariFlavour}/{platform}");
                }

                if(framework.Equals("net5.0"))
                {
                    var runtimes = XmlPeek(project, "Project/PropertyGroup/RuntimeIdentifiers").Split(';');
                    foreach(var runtime in runtimes)
                        RunPublish(runtime, runtime);
                }
                else
                {
                    RunPublish(null, "netfx");
                }
            }
            Verbose($"{publishDir}/{calamariFlavour}");
            Zip($"{publishDir}/{calamariFlavour}", $"{artifactsDir}{calamariFlavour}.zip");
        }
});

Task("PublishSashimiTestProjects")
    .IsDependentOn("Build")
    .Does(() => {
        var projects = GetFiles("./source/**/Sashimi.Tests.csproj");
		foreach(var project in projects)
        {
            var sashimiFlavour = XmlPeek(project, "Project/PropertyGroup/AssemblyName");

                void RunPublish() {
                     DotNetCorePublish(project.FullPath, new DotNetCorePublishSettings
		    	    {
		    	    	Configuration = configuration,
                        OutputDirectory = $"{publishDir}/{sashimiFlavour}"
		    	    });

                    CopyFiles("./global.json", $"{publishDir}/{sashimiFlavour}");
                }

                RunPublish();

            Verbose($"{publishDir}/{sashimiFlavour}");
            Zip($"{publishDir}/{sashimiFlavour}", $"{artifactsDir}{sashimiFlavour}.zip");
        }
});

Task("PackSashimi")
    .IsDependentOn("PublishSashimiTestProjects")
    .IsDependentOn("PublishCalamariProjects")
    .Does(() =>
{
    SignAndTimestampBinaries("./source/Sashimi/obj/Release/net5.0");
    DotNetCorePack("source", new DotNetCorePackSettings
    {
        Configuration = configuration,
        OutputDirectory = artifactsDir,
        NoBuild = false, // Don't change this flag we need it because of https://github.com/dotnet/msbuild/issues/5566
        IncludeSource = true,
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    });
    
    DeleteFiles(artifactsDir + "*symbols*");
});

Task("CopyToLocalPackages")
    .IsDependentOn("Test")
    .IsDependentOn("PackSashimi")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .Does(() =>
{
    CreateDirectory(localPackagesDir);
    CopyFiles(Path.Combine(artifactsDir, $"Sashimi.*.{nugetVersion}.nupkg"), localPackagesDir);
});

Task("Default")
    .IsDependentOn("CopyToLocalPackages");

void SignAndPack(string project, string binPath, DotNetCorePackSettings dotNetCorePackSettings){
    Information("SignAndPack project: " + project);
    Information("SignAndPack bin path: " + binPath);

    SignAndTimestampBinaries(binPath);

    DotNetCorePack(project, dotNetCorePackSettings);
}

private void SignAndTimestampBinaries(string outputDirectory)
{
    if (BuildSystem.IsLocalBuild && !signFiles) return;

    Information($"Signing binaries in {outputDirectory}");

    // check that any unsigned libraries, that Octopus Deploy authors, get signed to play nice with security scanning tools
    // refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
    // decision re: no signing everything: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1557938890227100
    var unsignedExecutablesAndLibraries =
        GetFiles(outputDirectory + "/{Calamari,Sashimi}*.{exe,dll}")
        .Where(f => !HasAuthenticodeSignature(f))
        .ToArray();

    Information("Signing files using azuresigntool and the production code signing certificate");
    SignFilesWithAzureSignTool(unsignedExecutablesAndLibraries, keyVaultUrl, keyVaultAppId, keyVaultAppSecret, keyVaultCertificateName);

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
    {
        Information("Adding file to sign: " + file.FullPath);
        signArguments.AppendQuoted(file.FullPath);
    }

    var azureSignToolPath = MakeAbsolute(File("./tools/azuresigntool.exe"));

    if (!FileExists(azureSignToolPath))
        throw new Exception($"The azure signing tool was expected to be at the path '{azureSignToolPath}' but wasn't available.");

  Information($"Executing: {azureSignToolPath} {signArguments.RenderSafe()}");
  var exitCode = StartProcess(azureSignToolPath.FullPath, signArguments.Render());
    if (exitCode != 0)
        throw new Exception($"AzureSignTool failed with the exit code {exitCode}.");

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

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);

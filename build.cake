//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0-beta0007"
#addin "MagicChunks"

using Path = System.IO.Path;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var testFilter = Argument("where", "");
var framework = Argument("framework", "");
var forceCiBuild = Argument("forceCiBuild", false);

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var artifactsDir = "./built-packages/";
var sourceFolder = "./source/";
var projectsToPackage = new []{"Calamari", "Calamari.Azure"};
var isContinuousIntegrationBuild = !BuildSystem.IsLocalBuild || forceCiBuild;

var gitVersionInfo = GitVersion(new GitVersionSettings {
    OutputType = GitVersionOutput.Json
});

var nugetVersion = isContinuousIntegrationBuild ? gitVersionInfo.NuGetVersion : "0.0.0";

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
    Information("Building Calamari v{0}", nugetVersion);
});

Teardown(context =>
{
    Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
//  PRIVATE TASKS
//////////////////////////////////////////////////////////////////////

Task("__Default")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__UpdateAssemblyVersionInformation")
    .IsDependentOn("__Build")
    .IsDependentOn("__Test")
    .IsDependentOn("__Pack")
    .IsDependentOn("__Publish");

Task("__Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
    CleanDirectories("./**/bin");
    CleanDirectories("./**/obj");
});

Task("__Restore")
    .Does(() => DotNetCoreRestore());

Task("__UpdateAssemblyVersionInformation")
    .WithCriteria(isContinuousIntegrationBuild)
    .Does(() =>
{
    foreach (var project in projectsToPackage)
    {
        GitVersion(new GitVersionSettings {
            UpdateAssemblyInfo = true,
            UpdateAssemblyInfoFilePath = Path.Combine(sourceFolder, project, "Properties", "AssemblyInfo.cs")
        });
    }
    
    Information("AssemblyVersion -> {0}", gitVersionInfo.AssemblySemVer);
    Information("AssemblyFileVersion -> {0}", $"{gitVersionInfo.MajorMinorPatch}.0");
    Information("AssemblyInformationalVersion -> {0}", gitVersionInfo.InformationalVersion);
});

Task("__Build")
    .Does(() =>
{
    var settings =  new DotNetCoreBuildSettings
    {
        Configuration = configuration
    };

    if(!string.IsNullOrEmpty(framework))
        settings.Framework = framework;      

    DotNetCoreBuild("**/project.json", settings);
});

Task("__BuildAndZipNET45TestProject")
    .Does(() => {
        var settings =  new DotNetCoreBuildSettings
        {
            Configuration = "Release",
            Framework = "net451",
            Runtime = "win7-x64"
        };

        DotNetCoreBuild("source/Calamari.Tests/project.json", settings);
        Zip("source/Calamari.Tests/bin/Release/net451/win7-x64/", Path.Combine(artifactsDir, "Binaries.zip"));
    });

Task("__Test")
    .Does(() =>
{
    var settings =  new DotNetCoreTestSettings
    {
        Configuration = configuration
    };

    if(!string.IsNullOrEmpty(framework))
        settings.Framework = framework;  

    if(!string.IsNullOrEmpty(testFilter))
        settings.ArgumentCustomization = f => {
            f.Append("-where");
            f.AppendQuoted(testFilter);
            return f;
        };

     DotNetCoreTest("source/Calamari.Tests/project.json", settings);
});

Task("__TestTeamCity")
    .Does(() =>
{
    // Run all Windows Tests
    // Runs two frameworks seperately to capture both outputs
    var settings =  new DotNetCoreTestSettings
    {
        Configuration = configuration
    };

    settings.ArgumentCustomization = f => {
        f.Append("-where");
        f.AppendQuoted("cat != Nix && cat != macOS");
        return f;
    };

    settings.Framework = "net451";
    DotNetCoreTest("./source/Calamari.Tests", settings);
    MoveFile("./TestResult.xml", "./TestResult.net451.xml");

    settings.Framework = "netcoreapp1.0";
    DotNetCoreTest("./source/Calamari.Tests", settings);
    MoveFile("./TestResult.xml", "./TestResult.netcoreapp1.0.xml");
});

Task("__Pack")
    .Does(() =>
{
    DoPackage("Calamari", "net40", nugetVersion);
    DoPackage("Calamari.Azure", "net45", nugetVersion);   
});

private void DoPackage(string project, string framework, string version)
{
    DotNetCorePublish(Path.Combine("./source", project), new DotNetCorePublishSettings
    {
        Configuration = configuration,
        OutputDirectory = Path.Combine(artifactsDir, project),
        Framework = framework
    });

    TransformConfig(Path.Combine(artifactsDir, project, "project.json"), new TransformationCollection {
        { "version", version }
    });

    DotNetCorePack(Path.Combine(artifactsDir, project), new DotNetCorePackSettings
    {
        OutputDirectory = artifactsDir,
        NoBuild = true
    });

    DeleteDirectory(Path.Combine(artifactsDir, project), true);
    DeleteFiles(artifactsDir + "*symbols*");
}

Task("__Publish")
    .WithCriteria(isContinuousIntegrationBuild && !forceCiBuild) //don't let publish criteria be overridden with flag
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

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .IsDependentOn("__Default");

Task("Clean")
    .IsDependentOn("__Clean");

Task("Restore")
    .IsDependentOn("__Restore");

Task("Build")
    .IsDependentOn("__Build");

Task("Pack")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__UpdateAssemblyVersionInformation")
    .IsDependentOn("__Build")
    .IsDependentOn("__Pack");

Task("Publish")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__UpdateAssemblyVersionInformation")
    .IsDependentOn("__Build")
    .IsDependentOn("__Pack")
    .IsDependentOn("__Publish");

Task("TeamCity")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__UpdateAssemblyVersionInformation")
    .IsDependentOn("__Build")
    .IsDependentOn("__TestTeamCity")
    .IsDependentOn("__Pack");   

Task("SetTeamCityVersion")
    .Does(() => {
        if(BuildSystem.IsRunningOnTeamCity)
            BuildSystem.TeamCity.SetBuildNumber(gitVersionInfo.NuGetVersion);
    });

Task("BuildPackAndZipTestBinaries")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__BuildAndZipNET45TestProject")
    .IsDependentOn("__Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);

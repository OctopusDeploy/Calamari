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

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var artifactsDir = "./built-packages/";
var sourceFolder = "./source/";
var projectsToPackage = new []{"Calamari", "Calamari.Azure"};
var isContinuousIntegrationBuild = !BuildSystem.IsLocalBuild;
var cleanups = new List<Action>();

var gitVersionInfo = GitVersion(new GitVersionSettings {
    OutputType = GitVersionOutput.Json
});

var nugetVersion = gitVersionInfo.NuGetVersion;

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
    Information("Building Calamari v{0}", nugetVersion);
});

Teardown(context =>
{
    Information("Cleaning up");
    foreach(var cleanup in cleanups)
        cleanup();

    Information("Finished running tasks for build v{0}", nugetVersion);
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
    .Does(() =>
{
    foreach (var project in projectsToPackage)
    {
        var assemblyInfoFile = Path.Combine(sourceFolder, project, "Properties", "AssemblyInfo.cs");
        RestoreFileOnCleanup(assemblyInfoFile);
        GitVersion(new GitVersionSettings {
            UpdateAssemblyInfo = true,
            UpdateAssemblyInfoFilePath = assemblyInfoFile
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
    .WithCriteria(isContinuousIntegrationBuild)
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

private void RestoreFileOnCleanup(string file)
{
    var contents = System.IO.File.ReadAllBytes(file);
    cleanups.Add(() => {
        Information("Restoring {0}", file);
        System.IO.File.WriteAllBytes(file, contents);
    });
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .IsDependentOn("BuildPackAndZipTestBinaries");

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

Task("SetTeamCityVersion")
    .Does(() => {
        if(BuildSystem.IsRunningOnTeamCity)
            BuildSystem.TeamCity.SetBuildNumber(gitVersionInfo.NuGetVersion);
    });

Task("BuildPackAndZipTestBinaries")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__UpdateAssemblyVersionInformation")
    .IsDependentOn("__BuildAndZipNET45TestProject")
    .IsDependentOn("__Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);

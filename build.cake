//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#module nuget:?package=Cake.DotNetTool.Module&version=0.4.0
#tool "dotnet:?package=GitVersion.Tool&version=5.3.6"
#tool "nuget:?package=TeamCity.Dotnet.Integration&version=1.0.10"
#tool "nuget:?package=NuGet.CommandLine&version=5.9.1"
#addin "nuget:?package=SharpZipLib&version=1.2.0"
#addin "nuget:?package=Cake.Compression&version=0.2.4"

using Path = System.IO.Path;
using IO = System.IO;
using Cake.Common.Xml;
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var publishDir = "./publish/";
var artifactsDir = "./artifacts/";
var localPackagesDir = "../LocalPackages";
var workingDir = "./working";
var projectUrl = "https://github.com/OctopusDeploy/Sashimi/";
var packagesFeed = "https://f.feedz.io/octopus-deploy/dependencies/nuget/index.json";

string nugetVersion;
GitVersion gitVersionInfo;

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
    CleanDirectory(workingDir);
    CleanDirectories("./source/**/bin");
    CleanDirectories("./source/**/obj");
});

Task("Build")
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
        var projects = GetFiles("./source/**/Calamari*.csproj");
		foreach(var project in projects)
        {
            var calamariFlavour = XmlPeek(project, "Project/PropertyGroup/AssemblyName") ?? project.GetFilenameWithoutExtension().ToString();

            var frameworks = XmlPeek(project, "Project/PropertyGroup/TargetFrameworks") ??
                XmlPeek(project, "Project/PropertyGroup/TargetFramework");

            foreach(var framework in frameworks.Split(';'))
            {
                void RunPublish(string runtime, string platform) {
                     DotNetCorePublish(project.FullPath, new DotNetCorePublishSettings
		    	    {
		    	    	Configuration = configuration,
                        OutputDirectory = $"{publishDir}/{calamariFlavour}/{platform}",
                        Framework = framework,
                        Runtime = runtime,
						NoRestore = true
		    	    });
                }

                if(framework.StartsWith("netcoreapp"))
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
            if (calamariFlavour.EndsWith(".Tests")) {
                TeamCity.PublishArtifacts($"{publishDir}{calamariFlavour}/**/*=>{calamariFlavour}.zip");
            } else {
                ZipCompress($"{publishDir}{calamariFlavour}", $"{artifactsDir}{calamariFlavour}.zip", 1);
            }
        }
});

Task("PublishSashimiTestProjects")
    .IsDependentOn("Build")
    .Does(() => {
        var projects = GetFiles("./source/**/Sashimi*.Tests.csproj");
		foreach(var project in projects)
        {
            var sashimiFlavour = XmlPeek(project, "Project/PropertyGroup/AssemblyName") ?? project.GetFilenameWithoutExtension().ToString();

            DotNetCorePublish(project.FullPath, new DotNetCorePublishSettings
            {
                Configuration = configuration,
                OutputDirectory = $"{publishDir}/{sashimiFlavour}"
            });

            Verbose($"{publishDir}/{sashimiFlavour}");
            TeamCity.PublishArtifacts($"{publishDir}{sashimiFlavour}/**/*=>{sashimiFlavour}.zip");
        }
});

Task("PackSashimi")
    .IsDependentOn("PublishSashimiTestProjects")
    .IsDependentOn("PublishCalamariProjects")
    .Does(() =>
{
    DotNetCorePack("source", new DotNetCorePackSettings
    {
        Configuration = configuration,
        OutputDirectory = artifactsDir,
        NoBuild = false, // Don't change this flag we need it because of https://github.com/dotnet/msbuild/issues/5566
        IncludeSource = false,
        IncludeSymbols = false,
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    });
});

Task("PublishPackageArtifacts")
    .IsDependentOn("PackSashimi")
    .Does(() =>
{
    var packages = GetFiles($"{artifactsDir}*.{nugetVersion}.nupkg");
    foreach (var package in packages)
    {
        TeamCity.PublishArtifacts(package.FullPath);
    }
});

Task("CopyToLocalPackages")
    .IsDependentOn("Test")
    .IsDependentOn("PublishPackageArtifacts")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .Does(() =>
{
    CreateDirectory(localPackagesDir);
    CopyFiles(Path.Combine(artifactsDir, $"*.{nugetVersion}.nupkg"), localPackagesDir);
});

Task("Publish")
    .IsDependentOn("Test")
    .IsDependentOn("PublishPackageArtifacts")
    .WithCriteria(BuildSystem.IsRunningOnTeamCity)
    .Does(() =>
{
    var packages = GetFiles($"{artifactsDir}*.{nugetVersion}.nupkg");
    foreach (var package in packages)
    {
        NuGetPush(package, new NuGetPushSettings {
            Source = packagesFeed,
            ApiKey = EnvironmentVariable("FeedzIoApiKey")
        });
    }
});

Task("Default")
    .IsDependentOn("CopyToLocalPackages")
    .IsDependentOn("Publish");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);

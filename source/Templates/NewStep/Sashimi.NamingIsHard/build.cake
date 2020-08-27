//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0"
#addin "Cake.FileHelpers&version=3.2.0"
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
var testing = Argument("testing", false);

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var publishDir = "./publish/";
var artifactsDir = "./artifacts/";
var localPackagesDir = "../LocalPackages";

string nugetVersion;


///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
    if (!DirectoryExists(".git")) {
        nugetVersion = "0.0.1-pre";
        return;
    }

    GitVersion gitVersionInfo = GitVersion(new GitVersionSettings {
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
void PublishArtifacts(string path) {
    if(!testing) {
        TeamCity.PublishArtifacts(path);
    }
}


Task("Clean")
    .Does(() =>
{
    CleanDirectory(publishDir);
    CleanDirectory(artifactsDir);
    CleanDirectories("./source/**/bin");
    CleanDirectories("./source/**/obj");
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
                     DotNetCorePublish(project.FullPath, new DotNetCorePublishSettings
		    	    {
		    	    	Configuration = configuration,
                        OutputDirectory = $"{publishDir}/{calamariFlavour}/{platform}",
                        Framework = framework,
                        Runtime = runtime
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
                PublishArtifacts($"{publishDir}{calamariFlavour}/**/*=>{calamariFlavour}.zip");
            } else {
                ZipCompress($"{publishDir}{calamariFlavour}", $"{artifactsDir}{calamariFlavour}.zip", 1);
                PublishArtifacts($"{artifactsDir}{calamariFlavour}.zip");
            }
        }
});

Task("PublishSashimiTestProjects")
    .IsDependentOn("Build")
    .Does(() => {
        var projects = GetFiles("./source/**/Sashimi.Tests.csproj");
		foreach(var project in projects)
        {
            var sashimiFlavour = XmlPeek(project, "Project/PropertyGroup/AssemblyName");

            DotNetCorePublish(project.FullPath, new DotNetCorePublishSettings
            {
                Configuration = configuration,
                OutputDirectory = $"{publishDir}/{sashimiFlavour}"
            });

            Verbose($"{publishDir}/{sashimiFlavour}");
            PublishArtifacts($"{publishDir}{sashimiFlavour}/**/*=>{sashimiFlavour}.zip");
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
        IncludeSource = true,
        IncludeSymbols = false,
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    });

    var packages = GetFiles($"{artifactsDir}*.{nugetVersion}.nupkg");
    foreach (var package in packages)
    {
        PublishArtifacts(package.FullPath);
    }
});

Task("CopyToLocalPackages")
    .IsDependentOn("Test")
    .IsDependentOn("PackSashimi")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .Does(() =>
{
    CreateDirectory(localPackagesDir);
    CopyFiles(Path.Combine(artifactsDir, $"*.{nugetVersion}.nupkg"), localPackagesDir);
});

Task("Default")
    .IsDependentOn("CopyToLocalPackages");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);

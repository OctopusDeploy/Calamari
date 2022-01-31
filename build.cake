//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#module nuget:?package=Cake.DotNetTool.Module&version=0.4.0
#tool "dotnet:?package=GitVersion.Tool&version=5.3.5"
#addin "Cake.FileHelpers&version=3.2.0"

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
    .WithCriteria(false)
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
            Zip($"{publishDir}{calamariFlavour}", $"{artifactsDir}{calamariFlavour}.zip");
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
            Zip($"{publishDir}{sashimiFlavour}", $"{artifactsDir}{sashimiFlavour}.zip");
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

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);

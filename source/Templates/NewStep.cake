//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////

using Path = System.IO.Path;
using IO = System.IO;
using Cake.Common.Xml;
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var destination = Argument<string>("destination");
var nugetVersion = Argument<string>("nugetVersion");
var source = Argument<string>("source");
var templatePath = Argument<string>("templatePath");
var artifactsDir = Argument<string>("artifactsDir");

//////////////////////////////////////////////////////////////////////
//  PRIVATE TASKS
//////////////////////////////////////////////////////////////////////
void RunDotnet(string command, string args)
{
	var exitCode = StartProcess("dotnet", $"{command} {args}");
    if (exitCode != 0) {
        throw new Exception($"dotnet {command} failed to execute");
    }
}

void DeleteEmptyFolders(string startLocation)
{
    foreach (var directory in IO.Directory.GetDirectories(startLocation))
    {
        DeleteEmptyFolders(directory);
        if (IO.Directory.GetFiles(directory).Length == 0 &&
            IO.Directory.GetDirectories(directory).Length == 0)
        {
            IO.Directory.Delete(directory, false);
        }
    }
}

Task("Build")
    .Does(() => {
        var templateSolutionPath = Path.Combine(destination, "Sashimi.NamingIsHard");
        EnsureDirectoryExists(templateSolutionPath);
        // Copy build scripts + others
        CopyFiles($"{source}/../*.*", templateSolutionPath);
        CleanDirectories($"{templatePath}/**/bin");
        CleanDirectories($"{templatePath}/**/obj");
        CopyDirectory(templatePath, destination);
        DeleteEmptyFolders(destination);
        RunDotnet($"{source}/Templates/Sashimi.Template.Wrangler/bin/Release/netcoreapp3.1/Sashimi.Template.Wrangler.dll", $"{nugetVersion} {templateSolutionPath}");
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
        var tempTestingPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        EnsureDirectoryExists(tempTestingPath);

        try {
            // Testing the template by invoking the cake script for the template
            // First we need to setup a temp location so we don't confuse GitVersion
            var testingDestination = Path.Combine(tempTestingPath, Path.GetRandomFileName());
            CopyDirectory(destination, testingDestination);

            // We also need the nuget packages we have just build to be available for this test to work, so we setup a local nuget repo
            var testingDestinationLocalPackage = Path.Combine(tempTestingPath, "LocalPackages");
            CreateDirectory(testingDestinationLocalPackage);
            CopyFiles(Path.Combine(artifactsDir, $"*.{nugetVersion}.nupkg"), testingDestinationLocalPackage);

            RunDotnet("nuget", $"add source --configfile \"{testingDestination}/Sashimi.NamingIsHard/nuget.config\" -n Local \"{testingDestinationLocalPackage}\"");

            CakeExecuteScript(Path.Combine(testingDestination, "Sashimi.NamingIsHard", "build.cake"), new CakeSettings {
                    Arguments = new Dictionary<string, string>{
                        {"testing", Boolean.TrueString}
                    }
                });
        } finally {
            DeleteDirectory(tempTestingPath, new DeleteDirectorySettings {
                                                     Recursive = true,
                                                     Force = true
                                                 });
        }
    });
Task("Default")
    .IsDependentOn("Test");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget("Default");
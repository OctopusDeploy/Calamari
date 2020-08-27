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

Task("Default")
    .Does(() => {
        CopyDirectory(templatePath, destination);

        var filesToDelete = GetFiles($"{destination}/**/*", new GlobberSettings { FilePredicate = (file) => {
                var filename = file.Path.GetFilename().FullPath;
                var extension = file.Path.GetExtension();

                if  (filename == "pom.xml"
                    || filename == "template.json"
                    || extension == ".kt"
                    || extension == ".kts") {
                    return false;
                }

                return true;
            }
        });
        DeleteFiles(filesToDelete);
        DeleteEmptyFolders(destination);
    });

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget("Default");
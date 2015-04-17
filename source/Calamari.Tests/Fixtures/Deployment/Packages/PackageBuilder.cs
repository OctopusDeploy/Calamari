using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Packages
{
    class PackageBuilder
    {
        public static string BuildSamplePackage(string name, string version, bool modifyPackage = false)
        {
            var currentDirectory = typeof(PackageBuilder).Assembly.FullLocalPath();
            var targetFolder = "source\\";
            var index = currentDirectory.LastIndexOf(targetFolder, StringComparison.OrdinalIgnoreCase);
            var solutionRoot = currentDirectory.Substring(0, index + targetFolder.Length);
            var nugetCommandLine = Path.Combine(solutionRoot, "packages\\NuGet.CommandLine.2.8.3\\tools\\NuGet.exe");
            Assert.That(File.Exists(nugetCommandLine));

            var packageDirectory = Path.Combine(solutionRoot, "Calamari.Tests\\Fixtures\\Deployment\\Packages\\" + name);
            Assert.That(Directory.Exists(packageDirectory));

            var nuspec = Path.Combine(packageDirectory, name + ".nuspec");
            Assert.That(File.Exists(nuspec));

            var output = Path.GetTempPath();
            var path = Path.Combine(output, name + "." + version + ".nupkg");
            if (File.Exists(path))
                File.Delete(path);

            string indexFilePath = null;
            if (modifyPackage)
            {
                indexFilePath = AddFileToPackage(packageDirectory);
            }

            var runner = new CommandLineRunner(new ConsoleCommandOutput());
            var result = runner.Execute(CommandLine.Execute(nugetCommandLine)
                .Action("pack")
                .Argument(nuspec)
                .Flag("NoPackageAnalysis")
                .Argument("Version", version)
                .Argument("OutputDirectory", output)
                .Build());
            result.VerifySuccess();

            if (modifyPackage
                && !String.IsNullOrWhiteSpace(indexFilePath) 
                && File.Exists(indexFilePath))
                File.Delete(indexFilePath);

            Assert.That(File.Exists(path));
            return path;
        }

        static string AddFileToPackage(string packageDirectory)
        {
            var indexFilePath = Path.Combine(packageDirectory, "index.html");
            var content = 
"<!DOCTYPE html>\n" +
"<html xmlns=\"http://www.w3.org/1999/xhtml\">\n" +
"<head>\n" +
"   <title>Acme.Web</title>\n" +
"</head>\n" +
"<body>\n" +
"   <h1>Welcome to Acme!</h1>\n" +
"   <h3>A Company that Makes Everything</h3>\n" +
"</body>\n" +
"</html>";

            using (var indexFile = File.CreateText(indexFilePath))
            {
                indexFile.Write(content);
                indexFile.Flush();
            }

            Assert.That(File.Exists(indexFilePath));
            return indexFilePath;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Integration.Processes;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Packages
{
    class PackageBuilder
    {
        public static string BuildSamplePackage(string name, string version)
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

            var runner = new CommandLineRunner(new ConsoleCommandOutput());
            var result = runner.Execute(CommandLine.Execute(nugetCommandLine)
                .Action("pack")
                .Argument(nuspec)
                .Flag("NoPackageAnalysis")
                .Argument("Version", version)
                .Argument("OutputDirectory", output)
                .Build());
            result.VerifySuccess();

            Assert.That(File.Exists(path));
            return path;
        }
    }
}

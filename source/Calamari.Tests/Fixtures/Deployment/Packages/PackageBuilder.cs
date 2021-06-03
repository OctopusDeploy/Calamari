using System;
using System.IO;
using System.IO.Compression;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;
using Calamari.Util;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Packages
{
    public class PackageBuilder
    {
        public static string BuildSamplePackage(string name, string version, bool modifyPackage = false)
        {
            var packageDirectory = TestEnvironment.GetTestPath("Fixtures", "Deployment", "Packages", name);
            Assert.That(Directory.Exists(packageDirectory), string.Format("Package {0} is not available (expected at {1}).", name, packageDirectory));

#if NETFX
            var nugetCommandLine = TestEnvironment.GetTestPath("NuGet", "NuGet.exe");
            Assert.That(File.Exists(nugetCommandLine), string.Format("NuGet.exe is not available (expected at {0}).", nugetCommandLine));

            var target = GetNixFileOrDefault(packageDirectory, name, ".nuspec");
#else
            var nugetCommandLine = "dotnet";

            var target = GetNixFileOrDefault(packageDirectory, name, ".csproj");
#endif

            var output = Path.Combine(Path.GetTempPath(), "CalamariTestPackages");
            Directory.CreateDirectory(output);
            var path = Path.Combine(output, name + "." + version + ".nupkg");
            if (File.Exists(path))
                File.Delete(path);


            var runner = new CommandLineRunner(ConsoleLog.Instance, new CalamariVariables());
#if NETCORE
            var restoreResult = runner.Execute(new CommandLine(nugetCommandLine)
                .Action("restore")
                .Argument(target)
                .Build());
            restoreResult.VerifySuccess();

#endif

            var result = runner.Execute(new CommandLine(nugetCommandLine)
                .Action("pack")
                .Argument(target)
#if NETFX
                .Argument("Version", version)
                .Flag("NoPackageAnalysis")
                .Argument("OutputDirectory", output)
#else
                .Argument("-output", output)
                .PositionalArgument("/p:Version=" + version)
#endif
                .Build());
            result.VerifySuccess();

            Assert.That(File.Exists(path), string.Format("The generated nupkg was unable to be found (expected at {0}).", path));
            return path;
        }

        static string GetNixFileOrDefault(string filePath, string filePrefix, string fileSuffix)
        {
            if (CalamariEnvironment.IsRunningOnWindows)
            {
                return GetWindowsOrDefaultPath();
            }
            else
            {
                var path = Path.Combine(filePath, filePrefix + ".Nix" + fileSuffix);
                return File.Exists(path)
                    ? path
                    : GetWindowsOrDefaultPath();
            }

            string GetWindowsOrDefaultPath()
            {
                var path = Path.Combine(filePath, filePrefix + fileSuffix);
                Assert.That(File.Exists(path), $"{fileSuffix} for {filePrefix} is not available (expected at {path}.");
                return path;
            }
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

        public static string BuildSimpleZip(string name, string version, string directory)
        {
            Assert.That(Directory.Exists(directory), string.Format("Package {0} is not available (expected at {1}).", name, directory));

            var output = Path.Combine(Path.GetTempPath(), "CalamariTestPackages");
            Directory.CreateDirectory(output);
            var path = Path.Combine(output, name + "." + version + ".zip");
            if (File.Exists(path))
                File.Delete(path);

            ZipFile.CreateFromDirectory(directory, path);

            return path;
        }
    }
}

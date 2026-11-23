using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Processes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using Calamari.Util;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Packages
{
    public static class PackageBuilder
    {
        static readonly Dictionary<(string Name, string Version), string> CachedSamplePackagePaths = [];

        public static string BuildSamplePackage(string name, string version)
        {
            var packageDirectory = TestEnvironment.GetTestPath("Fixtures", "Deployment", "Packages", name);
            Assert.That(Directory.Exists(packageDirectory), $"Package {name} is not available (expected at {packageDirectory}).");

            var output = Path.Combine(Path.GetTempPath(), "CalamariTestPackages");
            Directory.CreateDirectory(output);

            //if we haven't already built and cached this package, built it and c
            if (!CachedSamplePackagePaths.TryGetValue((name, version), out var samplePackagePath))
            {
                const string nugetCommandLine = "dotnet";
                var target = GetNixFileOrDefault(packageDirectory, name, ".csproj");

                var path = Path.Combine(output, name + "." + version + ".nupkg");

                var runner = new CommandLineRunner(ConsoleLog.Instance, new CalamariVariables());

                var result = runner.Execute(new CommandLine(nugetCommandLine)
                                            .Action("pack")
                                            .Argument(target)
                                            .Argument("-output", output)
                                            .PositionalArgument("/p:Version=" + version)
                                            .Build());

                result.VerifySuccess();

                Assert.That(File.Exists(path), $"The generated nupkg was unable to be found (expected at {path}).");

                //Store the built package path
                CachedSamplePackagePaths.Add((name, version), path);

                samplePackagePath = path;
            }

            //create a directory for this temp package
            var tmpDir = Path.Combine(output, Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(tmpDir);

            var fileName = Path.GetFileName(samplePackagePath);
            var tempFilePath = Path.Combine(tmpDir, fileName);

            File.Copy(samplePackagePath, tempFilePath);
            
            return tempFilePath;
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
                "<!DOCTYPE html>\n" + "<html xmlns=\"http://www.w3.org/1999/xhtml\">\n" + "<head>\n" + "   <title>Acme.Web</title>\n" + "</head>\n" + "<body>\n" + "   <h1>Welcome to Acme!</h1>\n" + "   <h3>A Company that Makes Everything</h3>\n" + "</body>\n" + "</html>";

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
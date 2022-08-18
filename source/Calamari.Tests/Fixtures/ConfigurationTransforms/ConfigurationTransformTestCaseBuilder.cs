using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Assent;
using Calamari.Common.Commands;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NSubstitute;
using NSubstitute.Core;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ConfigurationTransforms
{
    internal class ConfigurationTransformTestCaseBuilder
    {

        private readonly ICalamariFileSystem fileSystem;
        private string transformDefinition;
        private readonly List<string> files = new List<string>();
        private string[] mostRecentResult;
        private readonly Dictionary<string, string[]> allResults = new Dictionary<string, string[]>() ;
        private readonly string scenarioDescription;
        private string extractionDirectory;

        private ConfigurationTransformTestCaseBuilder(string description)
        {
            scenarioDescription = description;
            fileSystem = Substitute.For<ICalamariFileSystem>();
        }

        public static ConfigurationTransformTestCaseBuilder ForTheScenario(string description)
        {
            return new ConfigurationTransformTestCaseBuilder(description);
        }

        public ConfigurationTransformTestCaseBuilder Given => this;

        public ConfigurationTransformTestCaseBuilder And => this;

        public ConfigurationTransformTestCaseBuilder When => this;

        public ConfigurationTransformTestCaseBuilder Then => this;

        public ConfigurationTransformTestCaseBuilder Should => this;

        public ConfigurationTransformTestCaseBuilder FileExists(string fileName)
        {
            fileSystem.FileExists(fileName).Returns(true);
            var directory = Path.GetDirectoryName(fileName);
            fileSystem.DirectoryExists(directory).Returns(true);
            files.Add(fileName);
            return this;
        }

        public ConfigurationTransformTestCaseBuilder ExtractionDirectoryIs(string path)
        {
            extractionDirectory = path;
            return this;
        }

        public ConfigurationTransformTestCaseBuilder UsingTransform(string transform)
        {
            transformDefinition = transform;
            return this;
        }

        public ConfigurationTransformTestCaseBuilder SourceFile(string sourceFile)
        {
            Assert.IsTrue(fileSystem.FileExists(sourceFile), $"The sourceFile {sourceFile} should exist for this test");
            mostRecentResult = PerformTransform(sourceFile);
            allResults.Add(sourceFile, mostRecentResult);
            return this;
        }

        public ConfigurationTransformTestCaseBuilder FailToBeTransformed()
        {
            CollectionAssert.IsEmpty(mostRecentResult);
            return this;
        }

        public ConfigurationTransformTestCaseBuilder BeTransFormedBy(params string[] transformFiles)
        {
            CollectionAssert.AreEquivalent(transformFiles, mostRecentResult);
            return this;
        }

        private bool FilesMatch(CallInfo callInfo, string file)
        {
            var filePatterns = callInfo.ArgAt<string[]>(1);
            return filePatterns.Any(pattern =>
            {
                //bit of a naive regex, but it works enough for our tests
                //"foo.*.config" becomes "foo\..*\.config"
                var regex = new Regex(pattern.Replace(".", @"\.").Replace("*", ".*"));
                return regex.IsMatch(file);
            });
        }

        private string GetRelativePath(CallInfo callInfo, CalamariPhysicalFileSystem realFileSystem)
        {
            //nsubstitute calls the first return when you setup a second one. odd.
            if (callInfo.ArgAt<string>(0) == null)
                return null;
            return realFileSystem.GetRelativePath(callInfo.ArgAt<string>(0), callInfo.ArgAt<string>(1));
        }

        private string[] PerformTransform(string sourceFile)
        {
            fileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(callInfo => files.Where(file => FilesMatch(callInfo, file)));
            var realFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            fileSystem.GetRelativePath(Arg.Any<string>(), Arg.Any<string>())
                .Returns(x => GetRelativePath(x, realFileSystem));
            var transformFileLocator = new TransformFileLocator(fileSystem, new InMemoryLog());
            var transform = new XmlConfigTransformDefinition(transformDefinition);

            var deploymentVariables = new CalamariVariables();
            deploymentVariables[KnownVariables.OriginalPackageDirectoryPath] = extractionDirectory;
            var deployment = new RunningDeployment(null, deploymentVariables);

            const bool diagnosticLoggingEnabled = false;
            var result = transformFileLocator.DetermineTransformFileNames(sourceFile, transform, diagnosticLoggingEnabled, deployment.CurrentDirectory).ToArray();
            return result;
        }

        private class FolderOrFile
        {
            public readonly List<FolderOrFile> Children = new List<FolderOrFile>();

            public FolderOrFile(string name = "")
            {
                Name = name;
            }

            public string Name { get; }

            public void Print(StringBuilder results)
            {
                PrintPretty("", true, false, results);
            }

            private void PrintPretty(string indent, bool first, bool last, StringBuilder results)
            {
                results.Append(indent);
                if (last)
                {
                    results.Append("└─");
                    indent += "  ";
                }
                else if (!first)
                {
                    results.Append("├─");
                    indent += "| ";
                }
                results.AppendLine(Name);

                for (var i = 0; i < Children.Count; i++)
                    Children[i].PrintPretty(indent, false, i == Children.Count - 1, results);
            }
        }

        private void PrintPackageStructure(StringBuilder results)
        {
            var rootFolder = ConvertToHierachy("Acme.Core.1.0.0.nupkg", @"c:\temp\", x => x.StartsWith(@"c:\temp"));
            results.AppendLine("Given a package which has the structure:");
            rootFolder.Print(results);
        }

        private void PrintFilesNotInPackage(StringBuilder results)
        {
            if (files.All(x => x.StartsWith(@"c:\temp")))
                return;

            var rootFolder = ConvertToHierachy(@"c:\", @"c:\", x => !x.StartsWith(@"c:\temp"));
            results.AppendLine("And the following files exist:");
            rootFolder.Print(results);
        }

        private FolderOrFile ConvertToHierachy(string rootName, string prefixToRemove, Func<string, bool> predicate)
        {
            var rootFolder = new FolderOrFile(rootName);
            files.Sort();

            foreach (var file in files.Where(predicate))
            {
                var relativeFileName = file.Replace(prefixToRemove, "");
                if (relativeFileName.Contains(@"\"))
                {
                    FolderOrFile folder;
                    var directory = GetDirectory(relativeFileName);
                    if (rootFolder.Children.Any(x => x.Name == directory))
                    {
                        folder = rootFolder.Children.First(x => x.Name == directory);
                    }
                    else
                    {
                        folder = new FolderOrFile(directory);
                        rootFolder.Children.Add(folder);
                    }
                    folder.Children.Add(new FolderOrFile(GetFile(relativeFileName)));
                }
                else
                {
                    rootFolder.Children.Add(new FolderOrFile(relativeFileName));
                }
            }
            return rootFolder;
        }

        private string GetDirectory(string relativePath)
        {
            return relativePath.Split('\\')[0];
        }

        private string GetFile(string relativePath)
        {
            return relativePath.Split('\\')[1];
        }

        public void Verify(object testFixture, [CallerMemberName] string testName = null, [CallerFilePath] string filePath = null)
        {
            var results = new StringBuilder();
            results.AppendLine(scenarioDescription);
            PrintPackageStructure(results);
            PrintFilesNotInPackage(results);
            if (allResults.Count(x => x.Value.Length > 0) == 0)
            {
                results.AppendLine("Then the transform " + transformDefinition + " will do nothing.");
            }
            else
            {
                results.AppendLine("Then the transform " + transformDefinition + " will:");
                foreach (var result in allResults)
                {
                    foreach (var transform in result.Value)
                    {
                        results.AppendLine(" - Apply the transform " + transform.Replace(@"c:\temp\", "") + " to file " + result.Key.Replace(@"c:\temp\", ""));
                    }
                }
            }

            testFixture.Assent(results.ToString(), AssentConfiguration.Default, testName, filePath);
        }

    }
}
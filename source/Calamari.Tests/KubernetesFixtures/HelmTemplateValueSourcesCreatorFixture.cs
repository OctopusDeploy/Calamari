using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Conventions;
using Calamari.Tests.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class HelmTemplateValueSourcesCreatorFixture
    {
        static readonly string RootDir = Path.Combine("root", "staging");

        [TestCaseSource(nameof(ParseTemplateValuesSourceTestData))]
        public void ParseTemplateValuesSources_CorrectlyParsesAndWritesAndOrdersFiles(string templateValuesSourcesJson, IEnumerable<string> expectedFilenames, Dictionary<string, string> expectedFileContent, Dictionary<string, string> extraVariables = null)
        {
            // Arrange
            var variables = new CalamariVariables
            {
                [SpecialVariables.Helm.TemplateValuesSources] = templateValuesSourcesJson,
                [KnownVariables.OriginalPackageDirectoryPath] = RootDir
            };
            foreach (var kvp in extraVariables ?? new Dictionary<string, string>())
            {
                variables.Add(kvp.Key, kvp.Value);
            }

            var deployment = new RunningDeployment(variables)
            {
                CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory
            };

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), Arg.Any<string>())
                      .Returns(ci => ci.ArgAt<string[]>(1)
                                       ?.Select(x => Path.Combine(deployment.CurrentDirectory, x))
                                       .ToArray());

            var receivedFileContents = new Dictionary<string, string>();
            fileSystem.When(fs => fs.WriteAllText(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Encoding>()))
                      .Do(ci =>
                          {
                              receivedFileContents.Add(ci.ArgAt<string>(0), ci.ArgAt<string>(1));
                          });

            // Act
            var filenames = HelmTemplateValueSourcesCreator.ParseTemplateValuesSources(deployment, fileSystem, new SilentLog());

            // Assert
            using (var _ = new AssertionScope())
            {
                filenames.Should().BeEquivalentTo(expectedFilenames.Select(f => Path.Combine(RootDir, f)));

                foreach (var kvp in expectedFileContent)
                {
                    var filenameWithPath = Path.Combine(RootDir, kvp.Key);
                    var expectedContent = kvp.Value;

                    if (receivedFileContents.TryGetValue(filenameWithPath, out var receivedContent))
                    {
                        expectedContent.Should().Be(receivedContent);
                        expectedContent.ToArray().Should().BeEquivalentTo(receivedContent.ToArray());
                        Encoding.UTF8.GetBytes(expectedContent).Should().BeEquivalentTo(Encoding.UTF8.GetBytes(receivedContent));
                    }

                    fileSystem.Received(1).WriteAllText(Arg.Is(filenameWithPath), Arg.Is(expectedContent));
                }
            }
        }

        public static IEnumerable<object> ParseTemplateValuesSourceTestData => new object[]
        {
            CreateTestCase("Single pair KeyValues source",
                           new[]
                           {
                               new HelmTemplateValueSourcesCreator.KeyValuesTemplateValuesSource
                               {
                                   Value = new Dictionary<string, object>
                                   {
                                       ["Value 1"] = "Test"
                                   }
                               }
                           },
                           new[] {  HelmTemplateValueSourcesCreator.GetKeyValuesFileName(0) },
                           new Dictionary<string, string>
                           {
                               //the key values converter adds an extra end of line
                               [HelmTemplateValueSourcesCreator.GetKeyValuesFileName(0)] = "Value 1: Test\r\n".ReplaceLineEndings(),
                           }),

            CreateTestCase("Multiple pairs KeyValues source",
                           new[]
                           {
                               new HelmTemplateValueSourcesCreator.KeyValuesTemplateValuesSource
                               {
                                   Value = new Dictionary<string, object>
                                   {
                                       ["Value 1"] = "Test",
                                       ["Value 2"] = 1234
                                   }
                               }
                           },
                           new[] { HelmTemplateValueSourcesCreator.GetKeyValuesFileName(0) },
                           new Dictionary<string, string>
                           {
                               [HelmTemplateValueSourcesCreator.GetKeyValuesFileName(0)] = @"Value 1: Test
Value 2: 1234
".ReplaceLineEndings(),
                           }),

            CreateTestCase("Simple Inline Yaml",
                           new[]
                           {
                               new HelmTemplateValueSourcesCreator.InlineYamlTemplateValuesSource
                               {
                                   Value = @"it:
  is:
    some: 'yaml'"
                               },
                           },
                           new[] { HelmTemplateValueSourcesCreator.GetInlineYamlFileName(0) },
                           new Dictionary<string, string>
                           {
                               [HelmTemplateValueSourcesCreator.GetInlineYamlFileName(0)] = @"it:
  is:
    some: 'yaml'",
                           }),

            CreateTestCase("Simple Chart",
                           new[]
                           {
                               new HelmTemplateValueSourcesCreator.ChartTemplateValuesSource
                               {
                                   ValuesFilePaths = @"secondary.yaml
secondary.Development.yaml"
                               },
                           },
                           new[] { "secondary.yaml", "secondary.Development.yaml" },
                           new Dictionary<string, string>(), //no files get written as part of this one
                           new Dictionary<string, string>
                           {
                               [PackageVariables.IndexedPackageId(string.Empty)] = "mychart",
                               [PackageVariables.IndexedPackageVersion(string.Empty)] = "0.3.8"
                           }),

            CreateTestCase("Simple Package",
                           new[]
                           {
                               new HelmTemplateValueSourcesCreator.PackageTemplateValuesSource
                               {
                                   PackageName = "ValuesPack-1",
                                   PackageId = "customvalues",
                                   ValuesFilePaths = $@"{Path.Combine("dir", "values.yaml")}
{Path.Combine("dir2", "values.Development.yaml")}"
                               },
                           },
                           new[] { Path.Combine("dir", "values.yaml"), Path.Combine("dir2", "values.Development.yaml") },
                           new Dictionary<string, string>(), //no files get written as part of this one
                           new Dictionary<string, string>
                           {
                               [PackageVariables.IndexedPackageId("ValuesPack-1")] = "customvalues",
                               [PackageVariables.IndexedPackageVersion("ValuesPack-1")] = "2.0.0"
                           }),

            CreateTestCase("Correctly Ordered Output Filenames",
                           new HelmTemplateValueSourcesCreator.TemplateValuesSource[]
                           {
                               new HelmTemplateValueSourcesCreator.InlineYamlTemplateValuesSource
                               {
                                   Value = @"it:
  is:
    some: 'yaml'"
                               },
                               new HelmTemplateValueSourcesCreator.ChartTemplateValuesSource
                               {
                                   ValuesFilePaths = @"secondary.yaml
secondary.Development.yaml"
                               },
                               new HelmTemplateValueSourcesCreator.KeyValuesTemplateValuesSource
                               {
                                   Value = new Dictionary<string, object>
                                   {
                                       ["Value 1"] = "Test",
                                       ["Value 2"] = 1234
                                   }
                               },
                               new HelmTemplateValueSourcesCreator.InlineYamlTemplateValuesSource
                               {
                                   Value = @"yes: '1234'"
                               },
                               new HelmTemplateValueSourcesCreator.KeyValuesTemplateValuesSource
                               {
                                   Value = new Dictionary<string, object>
                                   {
                                       ["Value 3"] = "Testing",
                                   }
                               },
                           },
                           new[]
                           {
                               HelmTemplateValueSourcesCreator.GetKeyValuesFileName(4), 
                               HelmTemplateValueSourcesCreator.GetInlineYamlFileName(3),
                               HelmTemplateValueSourcesCreator.GetKeyValuesFileName(2), 
                               "secondary.yaml", 
                               "secondary.Development.yaml", 
                               HelmTemplateValueSourcesCreator.GetInlineYamlFileName(0),
                           },
                           new Dictionary<string, string>
                           {
                               [HelmTemplateValueSourcesCreator.GetInlineYamlFileName(0)] = @"it:
  is:
    some: 'yaml'",
                               [HelmTemplateValueSourcesCreator.GetKeyValuesFileName(2)] = @"Value 1: Test
Value 2: 1234
".ReplaceLineEndings(),
                               [HelmTemplateValueSourcesCreator.GetInlineYamlFileName(3)] = "yes: '1234'",
                               [HelmTemplateValueSourcesCreator.GetKeyValuesFileName(4)] = @"Value 3: Testing
".ReplaceLineEndings()
                           },
                           new Dictionary<string, string>
                           {
                               [PackageVariables.IndexedPackageId(string.Empty)] = "mychart",
                               [PackageVariables.IndexedPackageVersion(string.Empty)] = "0.3.8"
                           }),
        };

        static TestCaseData CreateTestCase(
            string testName,
            IEnumerable<HelmTemplateValueSourcesCreator.TemplateValuesSource> sources,
            IEnumerable<string> expectedFilenames,
            Dictionary<string, string> expectedFileContent,
            Dictionary<string, string> extraVariables = null)
        {
            return new TestCaseData(JsonConvert.SerializeObject(sources, Formatting.None),
                                    expectedFilenames,
                                    expectedFileContent,
                                    extraVariables)
                .SetName(testName);
        }
    }
}
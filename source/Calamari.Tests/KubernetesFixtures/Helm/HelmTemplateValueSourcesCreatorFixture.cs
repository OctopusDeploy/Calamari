using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Helm;
using Calamari.Tests.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Helm
{
    [TestFixture]
    public class HelmTemplateValueSourcesCreatorFixture
    {
        static readonly string RootDir = Path.Combine("root", "staging");

        [Test]
        public void ParseTemplateValuesSources_ChartSourceButIncorrectScriptSource_ShouldThrowArgumentException()
        {
            // Arrange
            var templateValuesSourcesJson = JsonConvert.SerializeObject(new HelmTemplateValueSourcesParser.TemplateValuesSource[]
                                                                        {
                                                                            new HelmTemplateValueSourcesParser.ChartTemplateValuesSource
                                                                            {
                                                                                ValuesFilePaths = "secondary.yaml"
                                                                            }
                                                                        },
                                                                        Formatting.None);

            var variables = new CalamariVariables
            {
                [SpecialVariables.Helm.TemplateValuesSources] = templateValuesSourcesJson,
                [KnownVariables.OriginalPackageDirectoryPath] = RootDir,
                [ScriptVariables.ScriptSource] = ScriptVariables.ScriptSourceOptions.Core,
                [PackageVariables.IndexedPackageId(string.Empty)] = "mychart",
                [PackageVariables.IndexedPackageVersion(string.Empty)] = "0.3.8"
            };

            var deployment = new RunningDeployment(variables)
            {
                CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory
            };
            var fileSystem = Substitute.For<ICalamariFileSystem>();

            // Act
            Action act = () => HelmTemplateValueSourcesParser.ParseTemplateValuesSources(deployment, fileSystem, new SilentLog());

            // Assert
            act.Should().ThrowExactly<ArgumentException>();
        }
        
        [Test]
        public void ParseTemplateValuesSources_ChartSourceButNotScriptSourceVairable_ShouldThrowArgumentNullException()
        {
            // Arrange
            var templateValuesSourcesJson = JsonConvert.SerializeObject(new HelmTemplateValueSourcesParser.TemplateValuesSource[]
                                                                        {
                                                                            new HelmTemplateValueSourcesParser.ChartTemplateValuesSource
                                                                            {
                                                                                ValuesFilePaths = "secondary.yaml"
                                                                            }
                                                                        },
                                                                        Formatting.None);

            var variables = new CalamariVariables
            {
                [SpecialVariables.Helm.TemplateValuesSources] = templateValuesSourcesJson,
                [KnownVariables.OriginalPackageDirectoryPath] = RootDir,
                [PackageVariables.IndexedPackageId(string.Empty)] = "mychart",
                [PackageVariables.IndexedPackageVersion(string.Empty)] = "0.3.8"
            };

            var deployment = new RunningDeployment(variables)
            {
                CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory
            };
            var fileSystem = Substitute.For<ICalamariFileSystem>();

            // Act
            Action act = () => HelmTemplateValueSourcesParser.ParseTemplateValuesSources(deployment, fileSystem, new SilentLog());

            // Assert
            act.Should().ThrowExactly<ArgumentNullException>();
        }

        [Test]
        public void ParseTemplateValuesSources_ChartPackageAndInlineAndKeyValues_CorrectlyParsesAndWritesAndOrdersFiles()
        {
            // Arrange
            var templateValuesSourcesJson = JsonConvert.SerializeObject(new HelmTemplateValueSourcesParser.TemplateValuesSource[]
                                                                        {
                                                                            new HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource
                                                                            {
                                                                                Value = @"it:
  is:
    some: 'yaml'"
                                                                            },
                                                                            new HelmTemplateValueSourcesParser.ChartTemplateValuesSource
                                                                            {
                                                                                ValuesFilePaths = @"secondary.yaml
secondary.Development.yaml"
                                                                            },
                                                                            new HelmTemplateValueSourcesParser.KeyValuesTemplateValuesSource
                                                                            {
                                                                                Value = new Dictionary<string, object>
                                                                                {
                                                                                    ["Value 1"] = "Test",
                                                                                    ["Value 2"] = 1234
                                                                                }
                                                                            },
                                                                            new HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource
                                                                            {
                                                                                Value = @"yes: '1234'"
                                                                            },
                                                                            new HelmTemplateValueSourcesParser.KeyValuesTemplateValuesSource
                                                                            {
                                                                                Value = new Dictionary<string, object>
                                                                                {
                                                                                    ["Value 3"] = "Testing",
                                                                                }
                                                                            },
                                                                        },
                                                                        Formatting.None);

            var variables = new CalamariVariables
            {
                [SpecialVariables.Helm.TemplateValuesSources] = templateValuesSourcesJson,
                [KnownVariables.OriginalPackageDirectoryPath] = RootDir,
                [ScriptVariables.ScriptSource] = ScriptVariables.ScriptSourceOptions.Package,
                [PackageVariables.IndexedPackageId(string.Empty)] = "mychart",
                [PackageVariables.IndexedPackageVersion(string.Empty)] = "0.3.8"
            };

            var deployment = new RunningDeployment(variables)
            {
                CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory
            };

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), Arg.Any<string>())
                      .Returns(ci => ci.ArgAt<string[]>(1)
                                       ?.Select(x => Path.Combine(deployment.CurrentDirectory, x))
                                       .ToArray());

            // Act
            var filenames = HelmTemplateValueSourcesParser.ParseTemplateValuesSources(deployment, fileSystem, new SilentLog());

            // Assert
            using (var _ = new AssertionScope())
            {
                filenames.Should()
                         .BeEquivalentTo(new[]
                         {
                             KeyValuesValuesFileWriter.GetFileName(4),
                             InlineYamlValuesFileWriter.GetFileName(3),
                             KeyValuesValuesFileWriter.GetFileName(2),
                             "secondary.yaml",
                             "secondary.Development.yaml",
                             InlineYamlValuesFileWriter.GetFileName(0),
                         }.Select(f => Path.Combine(RootDir, f)));
            }
        }
        
        [Test]
        public void ParseTemplateValuesSources_ChartGitRepositoryAndInlineAndKeyValues_CorrectlyParsesAndWritesAndOrdersFiles()
        {
            // Arrange
            var templateValuesSourcesJson = JsonConvert.SerializeObject(new HelmTemplateValueSourcesParser.TemplateValuesSource[]
                                                                        {
                                                                            new HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource
                                                                            {
                                                                                Value = @"it:
  is:
    some: 'yaml'"
                                                                            },
                                                                            new HelmTemplateValueSourcesParser.ChartTemplateValuesSource
                                                                            {
                                                                                ValuesFilePaths = @"secondary.yaml
secondary.Development.yaml"
                                                                            },
                                                                            new HelmTemplateValueSourcesParser.KeyValuesTemplateValuesSource
                                                                            {
                                                                                Value = new Dictionary<string, object>
                                                                                {
                                                                                    ["Value 1"] = "Test",
                                                                                    ["Value 2"] = 1234
                                                                                }
                                                                            },
                                                                            new HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource
                                                                            {
                                                                                Value = @"yes: '1234'"
                                                                            },
                                                                            new HelmTemplateValueSourcesParser.KeyValuesTemplateValuesSource
                                                                            {
                                                                                Value = new Dictionary<string, object>
                                                                                {
                                                                                    ["Value 3"] = "Testing",
                                                                                }
                                                                            },
                                                                        },
                                                                        Formatting.None);

            var variables = new CalamariVariables
            {
                [SpecialVariables.Helm.TemplateValuesSources] = templateValuesSourcesJson,
                [KnownVariables.OriginalPackageDirectoryPath] = RootDir,
                [ScriptVariables.ScriptSource] = ScriptVariables.ScriptSourceOptions.GitRepository,
                [Deployment.SpecialVariables.GitResources.CommitHash(string.Empty)] = "abc123"
            };

            var deployment = new RunningDeployment(variables)
            {
                CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory
            };

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), Arg.Any<string>())
                      .Returns(ci => ci.ArgAt<string[]>(1)
                                       ?.Select(x => Path.Combine(deployment.CurrentDirectory, x))
                                       .ToArray());

            // Act
            var filenames = HelmTemplateValueSourcesParser.ParseTemplateValuesSources(deployment, fileSystem, new SilentLog());

            // Assert
            using (var _ = new AssertionScope())
            {
                filenames.Should()
                         .BeEquivalentTo(new[]
                         {
                             KeyValuesValuesFileWriter.GetFileName(4),
                             InlineYamlValuesFileWriter.GetFileName(3),
                             KeyValuesValuesFileWriter.GetFileName(2),
                             "secondary.yaml",
                             "secondary.Development.yaml",
                             InlineYamlValuesFileWriter.GetFileName(0),
                         }.Select(f => Path.Combine(RootDir, f)));
            }
        }
    }
}
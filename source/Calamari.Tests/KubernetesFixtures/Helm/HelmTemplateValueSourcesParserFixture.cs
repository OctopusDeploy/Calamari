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
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Helm
{
    [TestFixture]
    public class HelmTemplateValueSourcesParserFixture
    {
        static readonly string RootDir = Path.Combine("root", "staging");
        
        const string ExampleJson = @"{
  ""name"": ""Test User"",
  ""id"": 1
}";

        [Test]
        public void ParseTemplateValuesFilesFromAllSources_ChartSourceButIncorrectScriptSource_ShouldThrowArgumentException()
        {
            // Arrange
            var sut = new HelmTemplateValueSourcesParser(Substitute.For<ICalamariFileSystem>(), new SilentLog());
                
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

            // Act
            Action act = () => sut.ParseAndWriteTemplateValuesFilesFromAllSources(deployment);

            // Assert
            act.Should().ThrowExactly<ArgumentException>();
        }

        [Test]
        public void ParseTemplateValuesFilesFromAllSources_ChartSourceButNotScriptSourceVairable_ShouldThrowArgumentNullException()
        {
            // Arrange
            var sut = new HelmTemplateValueSourcesParser(Substitute.For<ICalamariFileSystem>(), new SilentLog());
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

            // Act
            Action act = () => sut.ParseAndWriteTemplateValuesFilesFromAllSources(deployment);

            // Assert
            act.Should().ThrowExactly<ArgumentNullException>();
        }

        [Test]
        public void ParseTemplateValuesFilesFromAllSources_ChartPackageAndInlineAndKeyValues_CorrectlyParsesAndWritesAndOrdersFiles()
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
            
            var sut = new HelmTemplateValueSourcesParser(fileSystem, new SilentLog());

            // Act
            var filenames = sut.ParseAndWriteTemplateValuesFilesFromAllSources(deployment);

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
        public void ParseTemplateValuesFilesFromAllSources_CorrectlyParsesWithVariableSubstitution()
        {
            // Arrange
            var templateValuesSourcesJson = JsonConvert.SerializeObject(new HelmTemplateValueSourcesParser.TemplateValuesSource[]
                                                                        {
                                                                            new HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource
                                                                            {
                                                                                Value = "colors: #{JsonArray}"
                                                                            },
                                                                            new HelmTemplateValueSourcesParser.ChartTemplateValuesSource
                                                                            {
                                                                                ValuesFilePaths = "values/#{Octopus.Environment.Name | ToLower}.yaml"
                                                                            },
                                                                            new HelmTemplateValueSourcesParser.KeyValuesTemplateValuesSource
                                                                            {
                                                                                Value = new Dictionary<string, object>
                                                                                {
                                                                                    ["#{MyKey}"] = "#{MyValue}",
                                                                                    ["KeyWithNumberValue"] = 3
                                                                                }
                                                                            },
                                                                            new HelmTemplateValueSourcesParser.PackageTemplateValuesSource
                                                                            {
                                                                                PackageId = "#{MyPackage.Id}",
                                                                                PackageName = "#{MyPackage.Name}",
                                                                                ValuesFilePaths = "packages/#{Octopus.Environment.Name | ToLower}.yaml"
                                                                            }
                                                                        },
                                                                        Formatting.None);
            
            var variables = new CalamariVariables
            {
                ["JsonArray"] = @"[""red"",""green"",""blue""]",
                ["Octopus.Environment.Name"] = "Dev",
                ["MyKey"] = "key-1",
                ["MyValue"] = "value-1",
                ["MyPackage.Name"] = "helm-package",
                ["MyPackage.Id"] = "5ec01a18",
                [SpecialVariables.Helm.TemplateValuesSources] = templateValuesSourcesJson,
                [KnownVariables.OriginalPackageDirectoryPath] = RootDir,
                [ScriptVariables.ScriptSource] = ScriptVariables.ScriptSourceOptions.GitRepository,
                [PackageVariables.IndexedPackageId(string.Empty)] = "mychart",
                [PackageVariables.IndexedPackageVersion(string.Empty)] = "0.3.8",
                [PackageVariables.PackageCollection] = @"[""helm-package""]",
                [PackageVariables.IndexedPackageId("helm-package")] = "5ec01a18"
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
            
            var sut = new HelmTemplateValueSourcesParser(fileSystem, new SilentLog());

            // Act
            var filenames = sut.ParseAndWriteTemplateValuesFilesFromAllSources(deployment);

            // Assert
            using (var _ = new AssertionScope())
            {
                var expectedKeyValueContent = @"key-1: value-1
KeyWithNumberValue: 3
".ReplaceLineEndings();
                
                var inlineTvsFilename = Path.Combine(RootDir, InlineYamlValuesFileWriter.GetFileName(0));
                var chartTvsFilename = Path.Combine(RootDir, "values/dev.yaml");
                var keyValuesTvsFilename = Path.Combine(RootDir, KeyValuesValuesFileWriter.GetFileName(2));
                var packageTvsFilename = Path.Combine(RootDir, "packages/dev.yaml");
                
                filenames.Should().BeEquivalentTo(inlineTvsFilename, chartTvsFilename, keyValuesTvsFilename, packageTvsFilename);
                
                fileSystem.Received().WriteAllText(inlineTvsFilename, @"colors: [""red"",""green"",""blue""]");
                fileSystem.Received().WriteAllText(keyValuesTvsFilename, expectedKeyValueContent);
            }
        }

        [Test]
        public void ParseTemplateValuesFilesFromAllSources_ChartGitRepositoryAndInlineAndKeyValues_CorrectlyParsesAndWritesAndOrdersFiles()
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
            
            var sut = new HelmTemplateValueSourcesParser(fileSystem, new SilentLog());

            // Act
            var filenames = sut.ParseAndWriteTemplateValuesFilesFromAllSources(deployment);

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
        public void ParseTemplateValuesFilesFromDependencies_ChartGitRepositoryAndInlineAndKeyValues_CorrectlyParsesAndOnlyIncludesFilesFromDependencies()
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
                                                                            new HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource
                                                                            {
                                                                                Value = @"#{MyExampleJson}"
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
                ["MyExampleJson"] = ExampleJson,
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

            var log = new InMemoryLog();
            
            var sut = new HelmTemplateValueSourcesParser(fileSystem, log);

            // Act
            var filenames = sut.ParseTemplateValuesFilesFromDependencies(deployment);

            // Assert
            using (var _ = new AssertionScope())
            {
                filenames.Should()
                         .BeEquivalentTo(new[]
                         {
                             "secondary.yaml",
                             "secondary.Development.yaml",
                         }.Select(f => Path.Combine(RootDir, f)));
                
                log.Messages.Should().Contain(msg => msg.FormattedMessage.StartsWith("Including values file"));
            }
        }
        
         [Test]
        public void ParseTemplateValuesFilesFromDependencies_ChartGitRepositoryAndInlineAndKeyValuesAndNoLogging_CorrectlyParsesAndOnlyIncludesFilesFromDependenciesAndDoesNotLogIncludedFiles()
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

            var log = new InMemoryLog();
            
            var sut = new HelmTemplateValueSourcesParser(fileSystem, log);

            // Act
            var filenames = sut.ParseTemplateValuesFilesFromDependencies(deployment, false);

            // Assert
            using (var _ = new AssertionScope())
            {
                filenames.Should()
                         .BeEquivalentTo(new[]
                         {
                             "secondary.yaml",
                             "secondary.Development.yaml",
                         }.Select(f => Path.Combine(RootDir, f)));

                log.Messages.Should().NotContain(msg => msg.FormattedMessage.StartsWith("Including values file"));
            }
        }
    }
}
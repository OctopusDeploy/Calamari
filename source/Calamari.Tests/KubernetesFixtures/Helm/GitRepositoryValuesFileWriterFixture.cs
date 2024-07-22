using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Kubernetes.Helm;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Helm
{
    [TestFixture]
    public class GitRepositoryValuesFileWriterFixture
    {
        RunningDeployment deployment;
        ICalamariFileSystem fileSystem;
        InMemoryLog log;

        [SetUp]
        public void SetUp()
        {
            deployment = new RunningDeployment(new CalamariVariables());
            
            fileSystem = Substitute.For<ICalamariFileSystem>();
            //we have no invalid names
            fileSystem.RemoveInvalidFileNameChars(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));
            
            log = new InMemoryLog();
        }

        [TestCase(null)]
        [TestCase("\r")]
        [TestCase("\n")]
        [TestCase("\r\n")]
        [TestCase("  \n   ")]
        public void FindChartValuesFiles_InvalidValuesFilePaths_ReturnsNull(string valuesFilePaths)
        {
            // Act
            var result = GitRepositoryValuesFileWriter.FindChartValuesFiles(deployment, fileSystem, log, valuesFilePaths);

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void FindChartValuesFiles_FileNotFoundInFilesystem_ThrowsCommandException()
        {
            // Arrange
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), Arg.Any<string[]>())
                      .Returns(new List<string>());

            //Act
            Action act = () => GitRepositoryValuesFileWriter.FindChartValuesFiles(deployment, fileSystem, log, "values.yaml");

            // Assert
            act.Should().ThrowExactly<CommandException>();
        }

        [Test]
        public void FindChartValuesFiles_FilesFoundInFilesystem_ReturnsFullyQualifiedPaths()
        {
            // Arrange
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), Arg.Any<string[]>())
                      .Returns(ci =>
                               {
                                   return ci.ArgAt<string[]>(1)
                                            .Select(filename => Path.Combine(ci.ArgAt<string>(0), filename))
                                            .ToList();
                               });

            //Act
            var result = GitRepositoryValuesFileWriter.FindChartValuesFiles(deployment, fileSystem, log, "values.yaml\n values.Development.yaml");

            // Assert
            result.Should()
                  .BeEquivalentTo(new List<string>
                  {
                      Path.Combine(deployment.CurrentDirectory, "values.yaml"),
                      Path.Combine(deployment.CurrentDirectory, "values.Development.yaml")
                  });
        }

        [Test]
        public void FindGitDependencyValueFiles_GitDependencyNameEmpty_ReturnsNullAndLogsVerboseMessage()
        {
            // Arrange
            deployment.Variables.Add(SpecialVariables.GitResources.CommitHash("MyRepo"), "123abc");

            // Act
            var result = GitRepositoryValuesFileWriter.FindGitDependencyValuesFiles(deployment,
                                                                                    fileSystem,
                                                                                    log,
                                                                                    null,
                                                                                    "values.yaml");

            // Assert
            result.Should().BeNull();
            log.Messages.Should().Contain(msg => msg.Level == InMemoryLog.Level.Verbose && msg.FormattedMessage.Contains("Sourcing secondary values files from primary git dependency is not supported"));
        }

        [Test]
        public void FindGitDependencyValueFiles_GitDependencyNameNotInVariables_ReturnsNull()
        {
            // Arrange
            deployment.Variables.Add(SpecialVariables.GitResources.CommitHash("MyRepo"), "123abc");

            // Act
            var result = GitRepositoryValuesFileWriter.FindGitDependencyValuesFiles(deployment,
                                                                                    fileSystem,
                                                                                    log,
                                                                                    "MyOtherRepo",
                                                                                    "values.yaml");

            // Assert
            result.Should().BeNull();
        }

        [TestCase(null)]
        [TestCase("\r")]
        [TestCase("\n")]
        [TestCase("\r\n")]
        [TestCase("  \n   ")]
        public void GitRepositoryValuesFileWriter_InvalidValuesFilePaths_ReturnsNull(string valuesFilePaths)
        {
            // Arrange
            deployment.Variables.Add(SpecialVariables.GitResources.CommitHash("MyRepo"), "123abc");

            // Act
            var result = GitRepositoryValuesFileWriter.FindGitDependencyValuesFiles(deployment,
                                                                                    fileSystem,
                                                                                    log,
                                                                                    "MyRepo",
                                                                                    valuesFilePaths);
            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void GitRepositoryValuesFileWriter_FileNotFoundInFilesystem_ThrowsCommandException()
        {
            // Arrange
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), Arg.Any<string[]>())
                      .Returns(new List<string>());

            deployment.Variables.Add(SpecialVariables.GitResources.CommitHash("MyRepo"), "123abc");

            //Act
            Action act = () => GitRepositoryValuesFileWriter.FindGitDependencyValuesFiles(deployment,
                                                                                          fileSystem,
                                                                                          log,
                                                                                          "MyRepo",
                                                                                          "values.yaml");

            // Assert
            act.Should().ThrowExactly<CommandException>();
        }

        [Test]
        public void FindGitDependencyValuesFiles_FilesFoundInFilesystem_ReturnsFullyQualifiedPaths()
        {
            // Arrange
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), Arg.Any<string[]>())
                      .Returns(ci =>
                               {
                                   return ci.ArgAt<string[]>(1)
                                            .Select(filename => Path.Combine(ci.ArgAt<string>(0), filename))
                                            .ToList();
                               });

            deployment.Variables.Add(SpecialVariables.GitResources.CommitHash("MyRepo"), "123abc");

            //Act
            var result = GitRepositoryValuesFileWriter.FindGitDependencyValuesFiles(deployment,
                                                                                    fileSystem,
                                                                                    log,
                                                                                    "MyRepo",
                                                                                    "values.yaml \n values.Development.yaml");

            // Assert
            result.Should()
                  .BeEquivalentTo(new List<string>
                  {
                      Path.Combine(deployment.CurrentDirectory, "MyRepo", "values.yaml"),
                      Path.Combine(deployment.CurrentDirectory, "MyRepo", "values.Development.yaml")
                  });
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Helm;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Helm
{
    [TestFixture]
    public class PackageValuesFileWriterFixture
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

        [Test]
        public void FindPackageValuesFiles_PackageNameNotInVariables_ReturnsNull()
        {
            // Arrange
            deployment.Variables.Add(PackageVariables.IndexedPackageId("MyPackage"), "Package-1");

            // Act
            var result = PackageValuesFileWriter.FindPackageValuesFiles(deployment,
                                                                        fileSystem,
                                                                        log,
                                                                        "values.yaml",
                                                                        "Package-2",
                                                                        "MyPackage2");

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void FindPackageValuesFiles_PackageIdDoesNotMatchPackageIdInVariables_ReturnsNull()
        {
            // Arrange
            deployment.Variables.Add(PackageVariables.IndexedPackageId("MyPackage"), "Package-1");

            // Act
            var result = PackageValuesFileWriter.FindPackageValuesFiles(deployment,
                                                                        fileSystem,
                                                                        log,
                                                                        "values.yaml",
                                                                        "Package-2",
                                                                        "MyPackage");

            // Assert
            result.Should().BeNull();
        }

        [TestCase(null)]
        [TestCase("\r")]
        [TestCase("\n")]
        [TestCase("\r\n")]
        [TestCase("  \n   ")]
        public void FindPackageValuesFiles_InvalidValuesFilePaths_ReturnsNull(string valuesFilePaths)
        {
            // Arrange
            deployment.Variables.Add(PackageVariables.IndexedPackageId("MyPackage"), "Package-1");

            // Act
            var result = PackageValuesFileWriter.FindPackageValuesFiles(deployment,
                                                                        fileSystem,
                                                                        log,
                                                                        valuesFilePaths,
                                                                        "Package-1",
                                                                        "MyPackage");
            // Assert
            result.Should().BeNull();
        }

        [Test]
        public void FindPackageValuesFiles_FileNotFoundInFilesystem_ThrowsCommandException()
        {
            // Arrange
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), Arg.Any<string[]>())
                      .Returns(new List<string>());

            deployment.Variables.Add(PackageVariables.IndexedPackageId("MyPackage"), "Package-1");

            //Act
            Action act = () => PackageValuesFileWriter.FindPackageValuesFiles(deployment,
                                                                              fileSystem,
                                                                              log,
                                                                              "values.yaml",
                                                                              "Package-1",
                                                                              "MyPackage");

            // Assert
            act.Should().ThrowExactly<CommandException>();
        }

        [Test]
        public void FindPackageValuesFiles_FilesFoundInFilesystem_ReturnsFullyQualifiedPaths()
        {
            // Arrange
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), Arg.Any<string[]>())
                      .Returns(ci =>
                               {
                                   return ci.ArgAt<string[]>(1)
                                            .Select(filename => Path.Combine(ci.ArgAt<string>(0), filename))
                                            .ToList();
                               });

            deployment.Variables.Add(PackageVariables.IndexedPackageId("MyPackage"), "Package-1");

            //Act
            var result = PackageValuesFileWriter.FindPackageValuesFiles(deployment,
                                                                        fileSystem,
                                                                        log,
                                                                        "values.yaml \n values.Development.yaml",
                                                                        "Package-1",
                                                                        "MyPackage");

            // Assert
            result.Should()
                  .BeEquivalentTo(new List<string>
                  {
                      Path.Combine(deployment.CurrentDirectory, "MyPackage", "values.yaml"),
                      Path.Combine(deployment.CurrentDirectory, "MyPackage", "values.Development.yaml")
                  });
        }

        [Test]
        public void FindChartValuesFiles_FilesFoundInFilesystem_ReturnsFullyQualifiedPathsFromStagingRoot()
        {
            // Arrange
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), Arg.Any<string[]>())
                      .Returns(ci =>
                               {
                                   return ci.ArgAt<string[]>(1)
                                            .Select(filename => Path.Combine(ci.ArgAt<string>(0), filename))
                                            .ToList();
                               });

            deployment.Variables.Add(PackageVariables.IndexedPackageId(""), "Package-1");

            //Act
            var result = PackageValuesFileWriter.FindChartValuesFiles(deployment,
                                                                      fileSystem,
                                                                      log,
                                                                      "values.yaml \n values.Development.yaml");

            // Assert
            result.Should()
                  .BeEquivalentTo(new List<string>
                  {
                      Path.Combine(deployment.CurrentDirectory, "values.yaml"),
                      Path.Combine(deployment.CurrentDirectory, "values.Development.yaml")
                  });
        }
    }
}
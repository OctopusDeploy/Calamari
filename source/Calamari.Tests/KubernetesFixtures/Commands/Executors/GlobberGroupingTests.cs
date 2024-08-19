using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Kubernetes.Commands;
using Calamari.Kubernetes.Commands.Executors;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Commands.Executors
{
    [TestFixture]
    public class GlobberGroupingTests
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        string StagingDirectory => Path.Combine(tempDirectory, "staging");
        string tempDirectory;
        string PackageDirectory => Path.Combine(tempDirectory, "staging", KubernetesDeploymentCommandBase.PackageDirectoryName);

        [Test]
        public void FilesInDirectoryWithGlob_StageAllFilesInDirectory()
        {
            var dirName = "dirA";
            var dirA = Path.Combine(PackageDirectory, dirName);
            CreateTemporaryTestFile(dirA);
            CreateTemporaryTestFile(dirA);
            
            var globber = new GlobberGrouping(fileSystem);

            var globDirectories = globber.Group(StagingDirectory,  new List<string>(){ "dirA/*"});
            globDirectories.Count().Should().Be(1);
            fileSystem.EnumerateFiles(globDirectories[0].Directory, globDirectories[0].Glob).Count().Should().Be(2);
        }

        [Test]
        public void GlobsAreSortedInOrderOfPattern()
        {
            CreateTemporaryTestFile( Path.Combine(PackageDirectory, "dirB"));
            CreateTemporaryTestFile( Path.Combine(PackageDirectory, "dirA"));
            CreateTemporaryTestFile( Path.Combine(PackageDirectory, "dirC"));

            var globber = new GlobberGrouping(fileSystem);
            var globDirectories = globber.Group(StagingDirectory, new List<string>(){"dirC/*", "dirB/*", "dirA/*"});
      
            globDirectories.Select(d => d.Glob).Should().BeEquivalentTo(new[] { "dirC/*", "dirB/*", "dirA/*" });
        }
        
        [Test]
        public void EmptyGlobPatternReturnsNoResults()
        {
            CreateTemporaryTestFile( Path.Combine(PackageDirectory, "dirB"));

            var globber = new GlobberGrouping(fileSystem);
            var globDirectories = globber.Group(StagingDirectory, new List<string>());

            globDirectories.Should().BeEmpty();
        }

        void CreateTemporaryTestFile(string directory)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, Guid.NewGuid() + ".tmp");
            using (fileSystem.OpenFile(path, FileMode.OpenOrCreate, FileAccess.Read))
            {
            }
        }
        
        
        [SetUp]
        public void Init()
        {
            tempDirectory = fileSystem.CreateTemporaryDirectory();
        }

        [TearDown]
        public void Cleanup()
        {
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }
    }
}
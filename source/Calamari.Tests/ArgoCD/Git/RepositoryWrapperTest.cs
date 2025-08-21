using System;
using System.IO;
using Calamari.ArgoCD.Git;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using LibGit2Sharp;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git
{
    [TestFixture]
    public class RepositoryWrapperTest
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        
        InMemoryLog log;
        string tempDirectory;
        string OriginPath => Path.Combine(tempDirectory, "origin");
        string repositoryPath = "repository";
        Repository bareOrigin;
        string branchName = "devBranch";

        RepositoryWrapper repository;
        
        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            tempDirectory = fileSystem.CreateTemporaryDirectory();

            bareOrigin = RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(branchName, OriginPath);

            var repositoryFactory = new RepositoryFactory(log, tempDirectory);
            var connection = new GitConnection(null, null, OriginPath, branchName);
            repository = repositoryFactory.CloneRepository(repositoryPath, connection);
        }
        
        [TearDown]
        public void Cleanup()
        {
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }
        
        string RepositoryRootPath => Path.Combine(tempDirectory, repositoryPath);
        
        [Test]
        public void StagingANonExistentFileThrowsException()
        {
            Action act = () => repository.StageFiles(new[] { "nonexistent.txt"});
            act.Should().Throw<LibGit2SharpException>().And.Message.Should().Contain("could not find ");
        }

        [Test]
        public void EmptyCommitReturnsFalse()
        {
            var result = repository.CommitChanges("There is no data to comm it");
            result.Should().BeFalse();
        }

        [Test]
        public void AttemptingToAddFilestartingWithDotSlashSucceeds()
        {
            //This is to highlight a behaviour of libGit2Sharp which we may run into
            string filename = "newFile.txt";
            File.WriteAllText(Path.Combine(RepositoryRootPath, filename), "");
            repository.StageFiles(new[] { $"./{filename}" });
        }
        
        [Test]
        public void StagingARealFileSucceedsAndCanBeCommittedAndPushed()
        {
            string filename = "newFile.txt";
            string fileContents = "Lorem ipsum dolor sit amet";
            File.WriteAllText(Path.Combine(RepositoryRootPath, filename), fileContents);
            repository.StageFiles(new[] { filename });
            repository.CommitChanges("There is no data to comm it").Should().BeTrue();
            repository.PushChanges(false, branchName);
            
            //ensure the remote contains the file
            var originFileContent = RepositoryHelpers.ReadFileFromBranch(bareOrigin, branchName, filename);
            originFileContent.Should().Be(fileContents);
        }
        
        [Test]
        public void CanPushTheHeadToAnyBranchNameOnRemote()
        {
            string filename = "newFile.txt";
            File.WriteAllText(Path.Combine(RepositoryRootPath, filename), "");
            repository.StageFiles(new[] { filename });
            repository.CommitChanges("There is no data to comm it").Should().BeTrue();
            repository.PushChanges(false, "arbitraryBranch1");
            repository.PushChanges(false, "arbitraryBranch2");
        }
    }
}
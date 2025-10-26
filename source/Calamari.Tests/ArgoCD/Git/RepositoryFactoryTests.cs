#if NET
using System;
using System.IO;
using System.Text;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using LibGit2Sharp;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git
{
    [TestFixture]
    public class RepositoryFactoryTests
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();

        InMemoryLog log;
        string tempDirectory;
        string OriginPath => Path.Combine(tempDirectory, "origin");
        Repository bareOrigin;
        GitBranchName branchName = new GitBranchName("devBranch");

        RepositoryFactory repositoryFactory;

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            tempDirectory = fileSystem.CreateTemporaryDirectory();

            bareOrigin = RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(branchName, OriginPath);

            repositoryFactory = new RepositoryFactory(log, fileSystem, tempDirectory, Substitute.For<IGitHubPullRequestCreator>());
        }

        [TearDown]
        public void Cleanup()
        {
            RepositoryTestHelpers.DeleteRepositoryDirectory(fileSystem, tempDirectory);
        }

        [Test]
        public void ThrowsExceptionIfUrlDoesNotExist()
        {
            var connection = new GitConnection("username",
                                               "password",
                                               "file://doesNotExist",
                                               branchName);

            Action action = () => repositoryFactory.CloneRepository("name", connection);

            action.Should().Throw<CommandException>().And.Message.Should().Contain("Failed to clone Git repository");
        }

        [Test]
        public void CanCloneAnExistingRepositoryWithExplicitBranchNameAndAssociatedFiles()
        {
            var filename = "firstFile.txt";
            var originalContent = "This is the file content";
            CreateCommitOnOrigin(branchName.Value, filename, originalContent);

            var connection = new GitConnection(null, null, OriginPath, branchName);
            var clonedRepository = repositoryFactory.CloneRepository("CanCloneAnExistingRepository", connection);

            clonedRepository.Should().NotBeNull();

            File.Exists(Path.Combine(clonedRepository.WorkingDirectory, filename)).Should().BeTrue();
            var fileContent = File.ReadAllText(Path.Combine(clonedRepository.WorkingDirectory, filename));
            fileContent.Should().Be(originalContent);
        }

        [Test]
        public void CanCloneAnExistingRepositoryAtHEADAndAssociatedFiles()
        {
            var filename = "firstFile.txt";
            var originalContent = "This is the file content";
            CreateCommitOnOrigin(RepositoryHelpers.MainBranchName, filename, originalContent);

            var connection = new GitConnection(null, null, OriginPath, new GitBranchName("HEAD"));
            var clonedRepository = repositoryFactory.CloneRepository("CanCloneAnExistingRepository", connection);

            clonedRepository.Should().NotBeNull();

            File.Exists(Path.Combine(clonedRepository.WorkingDirectory, filename)).Should().BeTrue();
            var fileContent = File.ReadAllText(Path.Combine(clonedRepository.WorkingDirectory, filename));
            fileContent.Should().Be(originalContent);
        }

        void CreateCommitOnOrigin(string branchName, string fileName, string content)
        {
            var message = $"Commit: Message";
            var signature = new Signature("Author", "author@place.com", DateTimeOffset.Now);

            var branch = bareOrigin.Branches[branchName];
            var treeDefinition = TreeDefinition.From(branch.Tip.Tree);
            var blobID = bareOrigin.ObjectDatabase.Write<Blob>(Encoding.UTF8.GetBytes((content)));
            treeDefinition.Add(fileName, blobID, Mode.NonExecutableFile);

            var tree = bareOrigin.ObjectDatabase.CreateTree(treeDefinition);
            var commit = bareOrigin.ObjectDatabase.CreateCommit(
                                                                signature,
                                                                signature,
                                                                message,
                                                                tree,
                                                                new[] { branch.Tip },
                                                                false);
            bareOrigin.Refs.UpdateTarget(branch.Reference, commit.Id);
        }
    }
}
#endif
using System;
using System.IO;
using System.Text;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Time;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using LibGit2Sharp;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git
{
    [TestFixture]
    [Category(TestCategory.RequiresOpenSsl3)]
    public class RepositoryFactoryTests
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();

        InMemoryLog log;
        string tempDirectory;
        string OriginPath => Path.Combine(tempDirectory, "origin");
        Repository bareOrigin;
        readonly GitBranchName branchName = GitBranchName.CreateFromFriendlyName("devBranch");

        RepositoryFactory repositoryFactory;

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            tempDirectory = fileSystem.CreateTemporaryDirectory();

            bareOrigin = RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(branchName, OriginPath);

            repositoryFactory = new RepositoryFactory(log, fileSystem, tempDirectory, new GitVendorPullRequestClientResolver(Array.Empty<IGitVendorPullRequestClientFactory>()), new SystemClock());
        }

        [TearDown]
        public void Cleanup()
        {
            RepositoryHelpers.DeleteRepositoryDirectory(fileSystem, tempDirectory);
        }

        [Test]
        public void ThrowsExceptionIfUrlDoesNotExist()
        {
            var connection = new HttpsGitConnection("username",
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
            CreateCommitOnOrigin(branchName, filename, originalContent);

            var connection = new HttpsGitConnection(null, null, OriginPath, branchName);
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

            var connection = new HttpsGitConnection(null, null, OriginPath, new GitHead());
            var clonedRepository = repositoryFactory.CloneRepository("CanCloneAnExistingRepository", connection);

            clonedRepository.Should().NotBeNull();

            File.Exists(Path.Combine(clonedRepository.WorkingDirectory, filename)).Should().BeTrue();
            var fileContent = File.ReadAllText(Path.Combine(clonedRepository.WorkingDirectory, filename));
            fileContent.Should().Be(originalContent);
        }

        [Test]
        public void CloningSshGitConnectionDoesNotResolveAPullRequestClientAndLogsVerboseMessage()
        {
            // Arrange
            var filename = "sshTest.txt";
            var content = "ssh test content";
            CreateCommitOnOrigin(branchName, filename, content);

            var mockResolver = Substitute.For<IGitVendorPullRequestClientResolver>();
            var factoryWithMockedResolver = new RepositoryFactory(log, fileSystem, tempDirectory, mockResolver, new SystemClock());

            var sshConnection = new SshGitConnection(
                username: "git",
                url: OriginPath,
                gitReference: branchName,
                privateKey: "private-key",
                publicKey: "public-key",
                passphrase: "passphrase");

            // Act
            factoryWithMockedResolver.CloneRepository("Clone_WithSshConnection", sshConnection);

            mockResolver.DidNotReceive().TryResolve(Arg.Any<IHttpsGitConnection>(), Arg.Any<ILog>(), Arg.Any<System.Threading.CancellationToken>());

            log.MessagesVerboseFormatted
               .Should().Contain(s => s.Contains("SSH authentication") && s.Contains("Git vendor functionality will not be available"));
        }

        void CreateCommitOnOrigin(GitBranchName branchName, string fileName, string content)
        {
            var message = $"Commit: Message";
            var signature = new Signature("Author", "author@place.com", DateTimeOffset.Now);

            var branch = bareOrigin.Branches[branchName.Value];
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

using System;
using System.IO;
using System.Text;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
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
            var connection = new GitConnection("username",
                                               "password",
                                               new Uri("file://doesNotExist"),
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

            var connection = new GitConnection(null, null, new Uri(OriginPath), branchName);
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

            var connection = new GitConnection(null, null, new Uri(OriginPath), new GitHead());
            var clonedRepository = repositoryFactory.CloneRepository("CanCloneAnExistingRepository", connection);

            clonedRepository.Should().NotBeNull();

            File.Exists(Path.Combine(clonedRepository.WorkingDirectory, filename)).Should().BeTrue();
            var fileContent = File.ReadAllText(Path.Combine(clonedRepository.WorkingDirectory, filename));
            fileContent.Should().Be(originalContent);
        }

        [Test]
        public void CreateCredentialsProvider_SshConnection_ReturnsSshCredentials()
        {
            var connection = new SshGitConnection("git", "private-key", "public-key", "passphrase", new Uri("ssh://github.com/Foo/Bar"), branchName);
            var provider = RepositoryFactory.CreateCredentialsProvider(connection);

            provider.Should().NotBeNull();
            var credentials = provider!("", "", SupportedCredentialTypes.Default);
            credentials.Should().BeOfType<SshUserKeyMemoryCredentials>();
            var sshCreds = (SshUserKeyMemoryCredentials)credentials;
            sshCreds.Username.Should().Be("git");
            sshCreds.PrivateKey.Should().Be("private-key");
            sshCreds.PublicKey.Should().Be("public-key");
            sshCreds.Passphrase.Should().Be("passphrase");
        }

        [Test]
        public void CreateCredentialsProvider_UsernamePasswordConnection_ReturnsUsernamePasswordCredentials()
        {
            var connection = new GitConnection("user", "pass", new Uri("https://github.com/Foo/Bar"), branchName);
            var provider = RepositoryFactory.CreateCredentialsProvider(connection);

            provider.Should().NotBeNull();
            var credentials = provider!("", "", SupportedCredentialTypes.Default);
            credentials.Should().BeOfType<UsernamePasswordCredentials>();
        }

        [Test]
        public void CreateCredentialsProvider_AnonymousConnection_ReturnsNull()
        {
            var connection = new GitConnection(null, null, new Uri("https://github.com/Foo/Bar"), branchName);
            var provider = RepositoryFactory.CreateCredentialsProvider(connection);

            provider.Should().BeNull();
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

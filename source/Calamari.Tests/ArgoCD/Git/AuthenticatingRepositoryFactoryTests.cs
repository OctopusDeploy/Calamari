using System;
using System.IO;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.Time;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.Tests.ArgoCD.Git;

[Category(TestCategory.RequiresOpenSsl1_1OrOpenSsl3)]
public abstract class AuthenticatingRepositoryFactoryTestBase
{
    protected readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
    protected readonly GitBranchName branchName = GitBranchName.CreateFromFriendlyName("devBranch");

    protected InMemoryLog log;
    protected string tempDirectory;
    protected string OriginPath => Path.Combine(tempDirectory, "origin");
    protected RepositoryFactory repositoryFactory;

    [SetUp]
    public void Init()
    {
        log = new InMemoryLog();
        tempDirectory = fileSystem.CreateTemporaryDirectory();
        RepositoryHelpers.CreateBareRepository(OriginPath);
        RepositoryHelpers.CreateBranchIn(branchName, OriginPath);

        repositoryFactory = new RepositoryFactory(
            log,
            fileSystem,
            tempDirectory,
            new GitVendorPullRequestClientResolver([]),
            new SystemClock());
    }

    [TearDown]
    public void Cleanup()
    {
        RepositoryHelpers.DeleteRepositoryDirectory(fileSystem, tempDirectory);
    }

    [TestFixture]
    public class HttpsUrlTests : AuthenticatingRepositoryFactoryTestBase
    {
        [Test]
        public void HttpsCredentialIsSelectedWhenUrlMatchesHttpsCredential()
        {
            var httpsUrl = RepositoryHelpers.ToFileUri(OriginPath);
            var factory = new AuthenticatingRepositoryFactory(
                [new GitCredentialDto(httpsUrl, "", "")],
                repositoryFactory,
                log);

            using var wrapper = factory.CloneRepository(httpsUrl, branchName.ToFriendlyName());
            wrapper.Should().NotBeNull();
        }

        [Test]
        public void AnonymousCloneWhenNoCredentialsMatch()
        {
            var originUrl = RepositoryHelpers.ToFileUri(OriginPath);
            var factory = new AuthenticatingRepositoryFactory(
                [],
                repositoryFactory,
                log);

            using var wrapper = factory.CloneRepository(originUrl, branchName.ToFriendlyName());
            wrapper.Should().NotBeNull();
            log.Messages.Should().Contain(m => m.FormattedMessage.Contains("No Git credentials found"));
        }
    }

    [TestFixture]
    public class SshUrlTests : AuthenticatingRepositoryFactoryTestBase
    {
        [Test]
        // SSH not currently functional on Windows
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void SshCredentialBranch_IsSelectedAndDispatchesSshKeyGitConnection()
        {
            // Use an ssh:// URL so the new strict validation allows it, and mock the factory
            // so no real SSH connection is attempted.
            const string sshUrl = "ssh://git@github.com/org/repo.git";
            var mockRepoFactory = Substitute.For<IRepositoryFactory>();

            var factory = new AuthenticatingRepositoryFactory(
                [new SshKeyGitCredentialDto(sshUrl, "git", "private-key", [])],
                mockRepoFactory,
                log);

            factory.CloneRepository(sshUrl, branchName.ToFriendlyName());

            mockRepoFactory.Received()
                           .CloneRepository(
                               Arg.Any<string>(),
                               Arg.Is<IGitConnection>(c => c is SshKeyGitConnection));
        }

        [Test]
        public void HttpsCredentialTakesPriorityOverSshWhenBothMatchAnSshUrl()
        {
            AssertHttpsCredentialTakesPriorityOverSsh("ssh://git@github.com/org/repo.git");
        }

        [Test]
        // SSH not currently functional on Windows
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void KnownHostsFromDtoAreCarriedOntoSshKeyGitConnection()
        {
            const string sshUrl = "ssh://git@github.com/org/repo.git";
            var knownHosts = new[]
            {
                new SshKnownHostDto("github.com", "AAAAB3NzaC1yc2EAAAADAQABAAABAQ=="),
                new SshKnownHostDto("bitbucket.org", "AAAAC3NzaC1lZDI1NTE5AAAAIA==")
            };
            var mockRepoFactory = Substitute.For<IRepositoryFactory>();

            var factory = new AuthenticatingRepositoryFactory(
                [new SshKeyGitCredentialDto(sshUrl, "git", "private-key", knownHosts)],
                mockRepoFactory,
                log);

            factory.CloneRepository(sshUrl, branchName.ToFriendlyName());

            mockRepoFactory.Received()
                           .CloneRepository(
                               Arg.Any<string>(),
                               Arg.Is<IGitConnection>(c =>
                                   c is SshKeyGitConnection
                                   && ((SshKeyGitConnection)c).KnownHosts.Count == 2
                                   && ((SshKeyGitConnection)c).KnownHosts[0].Host == "github.com"
                                   && ((SshKeyGitConnection)c).KnownHosts[0].PublicKey == "AAAAB3NzaC1yc2EAAAADAQABAAABAQ=="
                                   && ((SshKeyGitConnection)c).KnownHosts[1].Host == "bitbucket.org"
                                   && ((SshKeyGitConnection)c).KnownHosts[1].PublicKey == "AAAAC3NzaC1lZDI1NTE5AAAAIA=="));
        }
    }

    [TestFixture]
    public class ScpStyleUrlTests : AuthenticatingRepositoryFactoryTestBase
    {
        [Test]
        public void ScpStyleUrlDoesNotMatchHttpsCredential()
        {
            // An SCP-style URL should not accidentally match an HTTPS credential for the same host
            var scpUrl = "git@github.com:org/repo.git";
            var httpsUrl = "https://github.com/org/repo.git";

            var factory = new AuthenticatingRepositoryFactory(
                [new GitCredentialDto(httpsUrl, "user", "pass")],
                repositoryFactory,
                log);

            // This will fail to clone (no real repo at this URL) but we can verify it
            // falls through to anonymous because the SCP URL doesn't match the HTTPS URL
            var act = () => factory.CloneRepository(scpUrl, "main");
            act.Should().Throw<Exception>(); // clone failure expected
            log.Messages.Should().Contain(m => m.FormattedMessage.Contains("No Git credentials found"));
        }

        [Test]
        public void HttpsCredentialTakesPriorityOverSshWhenBothMatchAnScpUrl()
        {
            AssertHttpsCredentialTakesPriorityOverSsh("git@github.com:org/repo.git");
        }
    }

    protected void AssertHttpsCredentialTakesPriorityOverSsh(string url)
    {
        var mockRepoFactory = Substitute.For<IRepositoryFactory>();

        // If there are HTTPS and SSH credentials for the same URL, HTTPS wins so API functionality works.
        IGitCredentialDto[] rawCredentials =
        [
            new GitCredentialDto(url, "https-user", "https-pass"),
            new SshKeyGitCredentialDto(url, "ssh-user", "private-key", [])
        ];

        var factory = new AuthenticatingRepositoryFactory(rawCredentials, mockRepoFactory, log);

        factory.CloneRepository(url, "main");

        mockRepoFactory.Received()
                       .CloneRepository(
                           Arg.Any<string>(),
                           Arg.Is<IGitConnection>(c => c is HttpsGitConnection));
    }
}
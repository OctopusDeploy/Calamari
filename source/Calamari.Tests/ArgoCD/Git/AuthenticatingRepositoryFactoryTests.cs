using System;
using System.IO;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
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
    protected IGitVendorClientResolver gitVendorClientResolver;

    [SetUp]
    public void Init()
    {
        log = new InMemoryLog();
        tempDirectory = fileSystem.CreateTemporaryDirectory();
        RepositoryHelpers.CreateBareRepository(OriginPath);
        RepositoryHelpers.CreateBranchIn(branchName, OriginPath);

        gitVendorClientResolver = Substitute.For<IGitVendorClientResolver>();
        repositoryFactory = new RepositoryFactory(
            log,
            fileSystem,
            tempDirectory,
            gitVendorClientResolver,
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

            using var wrapper = factory.CloneRepository(httpsUrl, branchName.ToFriendlyName(), false);
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

            using var wrapper = factory.CloneRepository(originUrl, branchName.ToFriendlyName(), false);
            wrapper.Should().NotBeNull();
            log.Messages.Should().Contain(m => m.FormattedMessage.Contains("No Git credentials found"));
        }
    }

    [TestFixture]
    public class SshUrlTests : AuthenticatingRepositoryFactoryTestBase
    {
        [Test]
        public void SshCredentialBranch_IsSelectedAndDispatchesSshKeyGitConnection()
        {
            const string sshUrl = "ssh://git@github.com/org/repo.git";
            var mockRepoFactory = Substitute.For<IRepositoryFactory>();

            var factory = new AuthenticatingRepositoryFactory(
                [new SshKeyGitCredentialDto(sshUrl, "git", "private-key", [])],
                mockRepoFactory,
                log);

            factory.CloneRepository(sshUrl, branchName.ToFriendlyName(), false);

            mockRepoFactory.Received()
                           .CloneRepository(
                               Arg.Any<string>(),
                               Arg.Is<IGitConnection>(c => c is SshKeyGitConnection));
        }

        [Test]
        public void HttpsAndSshForSameUrl_UsesSshForGitClone()
        {
            AssertSshIsUsedForGitClone("ssh://git@github.com/org/repo.git");
        }

        [Test]
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

            factory.CloneRepository(sshUrl, branchName.ToFriendlyName(), false);

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

        [Test]
        public void SshOnlyWithPullRequestRequired_ThrowsCommandException()
        {
            const string sshUrl = "ssh://git@github.com/org/repo.git";
            var mockRepoFactory = Substitute.For<IRepositoryFactory>();

            var factory = new AuthenticatingRepositoryFactory(
                [new SshKeyGitCredentialDto(sshUrl, "git", "private-key", [])],
                mockRepoFactory,
                log);

            var act = () => factory.CloneRepository(sshUrl, branchName.ToFriendlyName(), requiresPullRequest: true);

            act.Should().Throw<CommandException>().WithMessage("*Pull request creation is enabled*");
        }
    }

    [TestFixture]
    public class PullRequestRequiredTests : AuthenticatingRepositoryFactoryTestBase
    {
        [Test]
        public void NoCredentialMatchesUrl_ThrowsCommandException()
        {
            const string httpsUrl = "https://github.com/org/repo.git";
            var mockRepoFactory = Substitute.For<IRepositoryFactory>();

            var factory = new AuthenticatingRepositoryFactory(
                [],
                mockRepoFactory,
                log);

            var act = () => factory.CloneRepository(httpsUrl, branchName.ToFriendlyName(), requiresPullRequest: true);

            act.Should().Throw<CommandException>().WithMessage("*Pull request creation is enabled*");
            mockRepoFactory.DidNotReceiveWithAnyArgs().CloneRepository(default, default);
            mockRepoFactory.DidNotReceiveWithAnyArgs().CloneRepositoryWithPullRequestClient(default, default);
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
            var act = () => factory.CloneRepository(scpUrl, "main", false);
            act.Should().Throw<Exception>(); // clone failure expected
            log.Messages.Should().Contain(m => m.FormattedMessage.Contains("No Git credentials found"));
        }

        [Test]
        public void HttpsAndSshForSameUrl_UsesSshForGitClone_ScpUrl()
        {
            AssertSshIsUsedForGitClone("git@github.com:org/repo.git");
        }
    }

    protected void AssertSshIsUsedForGitClone(string url)
    {
        var mockRepoFactory = Substitute.For<IRepositoryFactory>();

        // When both kinds of credential are present for the same URL we route SSH to the git layer.
        IGitCredentialDto[] rawCredentials =
        [
            new GitCredentialDto(url, "https-user", "https-pass"),
            new SshKeyGitCredentialDto(url, "ssh-user", "private-key", [])
        ];

        var factory = new AuthenticatingRepositoryFactory(rawCredentials, mockRepoFactory, log);

        factory.CloneRepository(url, "main", false);

        mockRepoFactory.Received()
                       .CloneRepository(
                           Arg.Any<string>(),
                           Arg.Is<IGitConnection>(c => c is SshKeyGitConnection));
    }
}

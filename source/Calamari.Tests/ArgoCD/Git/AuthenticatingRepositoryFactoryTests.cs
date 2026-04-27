using System;
using System.Collections.Generic;
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
                new Dictionary<string, GitCredentialDto>
                {
                    [httpsUrl] = new GitCredentialDto(httpsUrl, "", "")
                },
                new Dictionary<string, GitCredentialSshKeyDto>(),
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
                new Dictionary<string, GitCredentialDto>(),
                new Dictionary<string, GitCredentialSshKeyDto>(),
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
        public void SshCredentialIsSelectedWhenUrlMatchesSshCredential()
        {
            var factory = new AuthenticatingRepositoryFactory(
                new Dictionary<string, GitCredentialDto>(),
                new Dictionary<string, GitCredentialSshKeyDto>
                {
                    // Use the local path as the SSH credential URL so the clone actually works
                    [OriginPath] = new GitCredentialSshKeyDto(OriginPath, "git", "private-key", "public-key", "passphrase")
                },
                repositoryFactory,
                log);

            using var wrapper = factory.CloneRepository(OriginPath, branchName.ToFriendlyName());
            wrapper.Should().NotBeNull();
        }

        [Test]
        public void HttpsCredentialTakesPriorityOverSshWhenBothMatchAnSshUrl()
        {
            AssertHttpsCredentialTakesPriorityOverSsh("ssh://git@github.com/org/repo.git");
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
                new Dictionary<string, GitCredentialDto>
                {
                    [httpsUrl] = new GitCredentialDto(httpsUrl, "user", "pass")
                },
                new Dictionary<string, GitCredentialSshKeyDto>(),
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

        var factory = new AuthenticatingRepositoryFactory(
            new Dictionary<string, GitCredentialDto>
            {
                [url] = new GitCredentialDto(url, "https-user", "https-pass")
            },
            new Dictionary<string, GitCredentialSshKeyDto>
            {
                [url] = new GitCredentialSshKeyDto(url, "ssh-user", "private-key", "public-key", "passphrase")
            },
            mockRepoFactory,
            log);

        factory.CloneRepository(url, "main");

        mockRepoFactory.Received()
                       .CloneRepository(
                           Arg.Any<string>(),
                           Arg.Is<IGitConnection>(c => c is HttpsGitConnection));
    }
}
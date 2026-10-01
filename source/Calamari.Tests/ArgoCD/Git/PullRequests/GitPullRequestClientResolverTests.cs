using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.ArgoCD.Git.PullRequests.Vendors.AzureDevOps;
using Calamari.ArgoCD.Git.PullRequests.Vendors.BitBucket;
using Calamari.ArgoCD.Git.PullRequests.Vendors.GitHub;
using Calamari.ArgoCD.Git.PullRequests.Vendors.GitLab;
using Calamari.Common.Plumbing.Logging;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git.PullRequests;

[TestFixture]
[Category("PlatformAgnostic")]
public class GitPullRequestClientResolverTests
{
    ILog log;
    IHttpsGitConnection connection;
    MemoryCache cache;

    [SetUp]
    public void SetUp()
    {
        log = Substitute.For<ILog>();
        connection = Substitute.For<IHttpsGitConnection>();
        connection.Username.Returns("test-user");
        connection.Password.Returns("test-token");
        cache = new MemoryCache(new MemoryCacheOptions());
    }

    [TearDown]
    public void TearDown()
    {
        cache.Dispose();
    }

    void ConfigureConnection(string url)
    {
        connection.Url.Returns(url);
        connection.Uri.Returns(new Lazy<Uri>(() => new Uri(url)));
    }

    GitVendorPullRequestClientResolver CreateResolverWithAllRealFactories()
    {
        var inspector = new SelfHostedGitLabInspector(cache);
        return new GitVendorPullRequestClientResolver([
            new GitHubPullRequestClientFactory(),
            new GitLabPullRequestClientFactory(inspector),
            new AzureDevOpsPullRequestClientFactory(),
            new BitBucketPullRequestClientFactory()
        ]);
    }

    [Test]
    public async Task GitHubUrl_ResolvesToGitHubClient()
    {
        ConfigureConnection("https://github.com/org/repo");
        var resolver = CreateResolverWithAllRealFactories();

        var client = await resolver.TryResolve(connection, log, CancellationToken.None);

        client.Should().BeOfType<GitHubPullRequestClient>();
    }

    [Test]
    public async Task GitLabCloudUrl_ResolvesToGitLabClient()
    {
        ConfigureConnection("https://gitlab.com/org/team/sub-team/repo.git");
        var resolver = CreateResolverWithAllRealFactories();

        var client = await resolver.TryResolve(connection, log, CancellationToken.None);

        client.Should().BeOfType<GitLabPullRequestClient>();
    }

    [Test]
    public async Task AzureDevOpsUrl_ResolvesToAzureDevOpsClient()
    {
        ConfigureConnection("https://dev.azure.com/org/project/_git/repo");
        var resolver = CreateResolverWithAllRealFactories();

        var client = await resolver.TryResolve(connection, log, CancellationToken.None);

        client.Should().BeOfType<AzureDevOpsPullRequestClient>();
    }

    [Test]
    public async Task BitBucketUrl_ResolvesToBitBucketClient()
    {
        ConfigureConnection("https://bitbucket.org/org/repo");
        var resolver = CreateResolverWithAllRealFactories();

        var client = await resolver.TryResolve(connection, log, CancellationToken.None);

        client.Should().BeOfType<BitBucketPullRequestClient>();
    }

    [Test]
    public async Task UnrecognisedUrl_ReturnsNull()
    {
        ConfigureConnection("https://someunknown.example/org/repo");
        var resolver = new GitVendorPullRequestClientResolver([
            new NeverMatchesFactory()
        ]);

        var client = await resolver.TryResolve(connection, log, CancellationToken.None);

        client.Should().BeNull();
    }

    [Test]
    public async Task SelfHostedUrl_WithMatchingSelfHostedFactory_ReturnsExpectedClient()
    {
        ConfigureConnection("https://mygitlab.company.com/org/repo");
        var expectedClient = Substitute.For<IGitVendorPullRequestClient>();
        var factory = Substitute.For<IGitVendorPullRequestClientFactory>();
        factory.CanHandleAsCloudHosted(Arg.Any<Uri>()).Returns(false);
        factory.CanHandleAsSelfHosted(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        factory.Create(connection, log, Arg.Any<CancellationToken>()).Returns(Task.FromResult(expectedClient));
        var resolver = new GitVendorPullRequestClientResolver([factory]);

        var client = await resolver.TryResolve(connection, log, CancellationToken.None);

        client.Should().Be(expectedClient);
    }

    /// <summary>Factory that never matches — used to test null return when no vendor is recognised.</summary>
    class NeverMatchesFactory : IGitVendorPullRequestClientFactory
    {
        public string Name => "NeverMatches";
        public bool CanHandleAsCloudHosted(Uri repositoryUri) => false;
        public Task<bool> CanHandleAsSelfHosted(Uri repositoryUri, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IGitVendorPullRequestClient> Create(IHttpsGitConnection repositoryConnection, ILog log, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}

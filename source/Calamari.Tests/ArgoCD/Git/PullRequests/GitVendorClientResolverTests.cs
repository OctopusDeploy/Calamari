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
public class GitVendorClientResolverTests
{
    ILog log;
    MemoryCache cache;

    [SetUp]
    public void SetUp()
    {
        log = Substitute.For<ILog>();
        cache = new MemoryCache(new MemoryCacheOptions());
    }

    [TearDown]
    public void TearDown()
    {
        cache.Dispose();
    }

    GitVendorClientResolver CreateResolverWithAllRealFactories()
    {
        var inspector = new SelfHostedGitLabInspector(cache);
        var factories = new IGitVendorPullRequestClientFactory[]
        {
            new GitHubPullRequestClientFactory(),
            new GitLabPullRequestClientFactory(inspector),
            new AzureDevOpsPullRequestClientFactory(),
            new BitBucketPullRequestClientFactory()
        };
        return new GitVendorClientResolver(factories);
    }

    [Test]
    public async Task GitHubUrl_ResolvesToGitHubClient()
    {
        var resolver = CreateResolverWithAllRealFactories();

        var client = await resolver.TryResolve(ConnectionFor("https://github.com/org/repo"), log, CancellationToken.None);

        client.Should().BeOfType<GitHubPullRequestClient>();
    }

    [Test]
    public async Task GitLabCloudUrl_ResolvesToGitLabClient()
    {
        var resolver = CreateResolverWithAllRealFactories();

        var client = await resolver.TryResolve(ConnectionFor("https://gitlab.com/org/repo"), log, CancellationToken.None);

        client.Should().BeOfType<GitLabPullRequestClient>();
    }

    [Test]
    public async Task AzureDevOpsUrl_ResolvesToAzureDevOpsClient()
    {
        var resolver = CreateResolverWithAllRealFactories();

        var client = await resolver.TryResolve(ConnectionFor("https://dev.azure.com/org/project/_git/repo"), log, CancellationToken.None);

        client.Should().BeOfType<AzureDevOpsPullRequestClient>();
    }

    [Test]
    public async Task BitBucketUrl_ResolvesToBitBucketClient()
    {
        var resolver = CreateResolverWithAllRealFactories();

        var client = await resolver.TryResolve(ConnectionFor("https://bitbucket.org/org/repo"), log, CancellationToken.None);

        client.Should().BeOfType<BitBucketPullRequestClient>();
    }

    [Test]
    public async Task UnrecognisedUrl_ReturnsNull()
    {
        var factories = new IGitVendorPullRequestClientFactory[] { new NeverMatchesFactory() };
        var resolver = new GitVendorClientResolver(factories);

        var client = await resolver.TryResolve(ConnectionFor("https://someunknown.example/org/repo"), log, CancellationToken.None);

        client.Should().BeNull();
    }

    [Test]
    public async Task SelfHostedUrl_WithMatchingSelfHostedFactory_ReturnsExpectedClient()
    {
        var connection = ConnectionFor("https://mygitlab.company.com/org/repo");
        var expectedClient = Substitute.For<IGitVendorPullRequestClient>();
        var factory = Substitute.For<IGitVendorPullRequestClientFactory>();
        factory.Name.Returns("GitLab");
        factory.CanHandleAsCloudHosted(Arg.Any<Uri>()).Returns(false);
        factory.CanHandleAsSelfHosted(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        factory.CreateForPullRequests(connection, log, Arg.Any<CancellationToken>()).Returns(Task.FromResult(expectedClient));
        var factories = new[] { factory };
        var resolver = new GitVendorClientResolver(factories);

        var client = await resolver.TryResolve(connection, log, CancellationToken.None);

        client.Should().Be(expectedClient);
    }

    static IHttpsGitConnection ConnectionFor(string url)
        => new HttpsGitConnection("test-user", "test-token", url, new GitHead());

    class NeverMatchesFactory : IGitVendorPullRequestClientFactory
    {
        public string Name => "GitHub";
        public bool CanHandleAsCloudHosted(Uri repositoryUri) => false;
        public Task<bool> CanHandleAsSelfHosted(Uri repositoryUri, CancellationToken cancellationToken) => Task.FromResult(false);
        public IGitVendorClient Create(IGitConnection repositoryConnection) => throw new NotImplementedException();
        public Task<IGitVendorPullRequestClient> CreateForPullRequests(IHttpsGitConnection repositoryConnection, ILog log, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}

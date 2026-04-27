using System;
using System.Linq;
using System.Text;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.Time;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Calamari.Tests.ArgoCD.Git;

[TestFixture]
public class GitHttpSmartSubTransportTests
{
    readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();

    InMemoryLog log;
    string tempDirectory;
    WireMockServer server;

    [SetUp]
    public void Init()
    {
        log = new InMemoryLog();
        tempDirectory = fileSystem.CreateTemporaryDirectory();
        server = WireMockServer.Start();

        // Return 200 for any request. The body is not valid git smart HTTP,
        // so the clone will fail after the request is sent — but WireMock
        // will have recorded the request headers we need to inspect.
        server.Given(Request.Create().UsingAnyMethod())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody("not-a-git-response"));
    }

    [TearDown]
    public void Cleanup()
    {
        server?.Stop();
        server?.Dispose();
        RepositoryHelpers.DeleteRepositoryDirectory(fileSystem, tempDirectory);
    }

    [Test]
    public void BasicAuthHeaderIsSentOnFirstRequest()
    {
        var username = "testuser";
        var password = "testpassword";
        var expectedAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        var repoUrl = $"{server.Url}/fake-repo.git";
        var connection = new HttpsGitConnection(username, password, repoUrl, GitBranchName.CreateFromFriendlyName("main"));
        var repositoryFactory = new RepositoryFactory(
            log,
            fileSystem,
            tempDirectory,
            new GitVendorPullRequestClientResolver([]),
            new SystemClock());

        // The clone will fail because WireMock doesn't speak git protocol,
        // but the HTTP request will have been sent and recorded.
        var act = () => repositoryFactory.CloneRepository("test-repo", connection);
        act.Should().Throw<CommandException>();

        var requests = server.LogEntries.ToList();
        requests.Should().NotBeEmpty("at least one HTTP request should have been made");

        var firstRequest = requests.First();
        firstRequest.RequestMessage.Headers.Should().ContainKey("Authorization");

        var authHeader = firstRequest.RequestMessage.Headers?["Authorization"].First();
        authHeader.Should().Be($"Basic {expectedAuth}",
            "the Basic auth header should be sent proactively on the first request, not after a 401 challenge");
    }

    [Test]
    public void NoAuthHeaderIsSentWhenCredentialsAreNotProvided()
    {
        var repoUrl = $"{server.Url}/fake-repo.git";
        var connection = new HttpsGitConnection(null, null, repoUrl, GitBranchName.CreateFromFriendlyName("main"));
        var repositoryFactory = new RepositoryFactory(
            log,
            fileSystem,
            tempDirectory,
            new GitVendorPullRequestClientResolver([]),
            new SystemClock());

        var act = () => repositoryFactory.CloneRepository("test-repo", connection);
        act.Should().Throw<CommandException>();

        var requests = server.LogEntries.ToList();
        requests.Should().NotBeEmpty("at least one HTTP request should have been made");

        var firstRequest = requests.First();
        firstRequest.RequestMessage.Headers.Should().NotContainKey("Authorization",
            "no auth header should be sent when credentials are not provided");
    }
}

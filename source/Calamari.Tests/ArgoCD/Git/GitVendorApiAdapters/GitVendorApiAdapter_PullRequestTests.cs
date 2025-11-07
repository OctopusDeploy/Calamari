#if NET
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using Calamari.Common.Plumbing.FileSystem;
using FluentAssertions;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using NGitLab;
using NSubstitute;
using NUnit.Framework;
using Octokit;
using Credentials = Octokit.Credentials;
using Repository = LibGit2Sharp.Repository;

namespace Calamari.Tests.ArgoCD.Git.GitVendorApiAdapters
{
    [TestFixture]
    public class GitHubApiAdapterTests
    {
        [Test]
        [Ignore("Test currently used for local development and debugging")]
        public async Task TestBitBucketChangeRequest()
        {
            var defaultBranch = "";
            var repositoryUrl = "";
            var cloneUsername = "";
            var clonePassword = "";


            await TestPullRequest(repositoryUrl,
                                  defaultBranch,
                                  cloneUsername,
                                  clonePassword,
                                  (conn) => new BitBucketApiAdapter(conn, new Uri("https://bitbucket.org")));
        }
        [Test]
        [Ignore("Test currently used for local development and debugging")]
        public async Task TestGitHubMergeRequest()
        {
            var defaultBranch = "";
            var repositoryUrl = "";
            var cloneUsername = "";
            var clonePassword = "";

            await TestPullRequest(repositoryUrl,
                                  defaultBranch,
                                  cloneUsername,
                                  clonePassword,
                                  (conn) =>
                                  {
                                      var credentials = new Credentials(cloneUsername, clonePassword);
                                      var client = new GitHubClient(new Connection(new ProductHeaderValue("octopus-deploy-test"))) { Credentials = credentials };
                                      return new GitHubApiAdapter(client, conn, new Uri("https://github.com/"));
                                  });
        }

        [Test]
        [Ignore("Test currently used for local development and debugging")]
        public async Task TestAzureDevopsPullRequest()
        {
            var defaultBranch = "";
            var repositoryUrl = "";
            var cloneUsername = "";
            var clonePassword = "";

            await TestPullRequest(repositoryUrl,
                                  defaultBranch,
                                  cloneUsername,
                                  clonePassword,
                                  (conn) => new AzureDevOpsApiAdapter(conn));
        }

        [Test]
        [Ignore("Test currently used for local development and debugging")]
        public async Task TestGitLabMergeRequest()
        {
            var defaultBranch = "";
            var repositoryUrl = "";
            var cloneUsername = "";
            var clonePassword = "";

            await TestPullRequest(repositoryUrl,
                                  defaultBranch,
                                  cloneUsername,
                                  clonePassword,
                                  (conn) =>
                                  {
                                      var client = new GitLabClient("https://gitlab.com", clonePassword);
                                      return new GitLabApiAdapter(client, conn, new Uri("https://gitlab.com"));
                                  });
        }

        async Task TestPullRequest(string repositoryUrl, string defaultBranch, string cloneUsername, string clonePassword, Func<IRepositoryConnection, IGitVendorApiAdapter> createVendorApiAdapter)
        {
            
            using var temporaryFolder = TemporaryDirectory.Create();
            
            CredentialsHandler credentialsHandler = (url, usernameFromUrl, types) => new UsernamePasswordCredentials { Username = cloneUsername, Password = clonePassword};
            var repositoryPath =  Repository.Clone(repositoryUrl, temporaryFolder.DirectoryPath, new CloneOptions()
            {
                FetchOptions =
                {
                    CredentialsProvider = credentialsHandler,
                }
            });

            using var repository = new Repository(repositoryPath);
            var newBranch = repository.Branches.Add($"test-{Guid.NewGuid():N}", defaultBranch);
            repository.AddFilesToBranch(new GitBranchName(newBranch.CanonicalName), ("file",$"NewFile ${DateTime.Now}"));
            var remote = repository.Network.Remotes.First();
            repository.Branches.Update(newBranch, branch => branch.Remote = remote.Name, branch => branch.UpstreamBranch = newBranch.CanonicalName);
            repository.Network.Push(newBranch, new PushOptions() { CredentialsProvider = credentialsHandler });

            var conn = Substitute.For<IRepositoryConnection>();
            conn.Url.Returns(new Uri(repositoryUrl));
            conn.Username.Returns(cloneUsername);
            conn.Password.Returns(clonePassword);
            try
            {
                var apiAdapter = createVendorApiAdapter(conn);
                var pullRequest = await apiAdapter.CreatePullRequest("MyTitle",
                                                                     "MyBody",
                                                                     new GitBranchName(newBranch.CanonicalName),
                                                                     new GitBranchName(defaultBranch),
                                                                     CancellationToken.None);
                pullRequest.Number.Should().BeGreaterThan(0);
            }finally
            {
                // Attempt to Delete Branch on Remote
                var pushRefSpec = $":{newBranch.CanonicalName}";
                repository.Network.Push(remote, pushRefSpec, new PushOptions()
                {
                    CredentialsProvider = credentialsHandler,
                });
                
                RepositoryHelpers.DeleteRepositoryDirectory(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), temporaryFolder.DirectoryPath);
            }
        }
    }
}
#endif
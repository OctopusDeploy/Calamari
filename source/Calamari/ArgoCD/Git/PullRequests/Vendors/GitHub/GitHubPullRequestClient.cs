#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Octokit;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.GitHub
{
    public class GitHubPullRequestClient : GitHubGitClient, IGitVendorPullRequestClient
    {
        readonly IGitHubClient client;

        public GitHubPullRequestClient(IGitHubClient client, IHttpsGitConnection repositoryConnection, Uri baseUrl)
            : base(repositoryConnection.Uri.Value, baseUrl)
        {
            this.client = client;
        }

        public async Task<PullRequest> CreatePullRequest(string pullRequestTitle,
                                                         string body,
                                                         GitBranchName sourceBranch,
                                                         GitBranchName destinationBranch,
                                                         CancellationToken cancellationToken)
        {
            var repo = await client.Repository.Get(repoOwner, repoName);

            var pr = await client.PullRequest.Create(repo.Id,
                                                     new NewPullRequest(pullRequestTitle,
                                                                        sourceBranch.Value,
                                                                        destinationBranch.Value)
                                                     {
                                                         Body = body
                                                     });

            return new PullRequest(pr.Title, pr.Number, pr.HtmlUrl);
        }
    }
}

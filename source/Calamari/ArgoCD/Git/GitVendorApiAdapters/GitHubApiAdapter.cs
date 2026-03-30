#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.GitHub;
using Octokit;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    public class GitHubApiAdapter: IGitVendorApiAdapter
    {
        readonly IGitHubClient client;
        readonly Uri baseUrl;
        readonly string repoOwner;
        readonly string repoName;

        public GitHubApiAdapter(IGitHubClient client, IRepositoryConnection repositoryConnection, Uri baseUrl)
        {
            this.client = client;
            this.baseUrl = baseUrl;

            var parts = repositoryConnection.Url.ExtractPropertiesFromUrlPath();
            repoOwner = parts[0];
            repoName = parts[1];
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

        public string GenerateCommitUrl(string commit)
        {
            //var commitInfo =  client.Repository.Commit.Get(repoOwner, repoName, commit).Result;
            //return commitInfo.HtmlUrl;
            return $"{baseUrl.AbsoluteUri}/{repoOwner}/{repoName}/commit/{commit}";
        }
    }
}
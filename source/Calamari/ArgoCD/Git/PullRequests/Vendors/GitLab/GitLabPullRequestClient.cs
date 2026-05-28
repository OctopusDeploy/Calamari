#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using NGitLab;
using NGitLab.Models;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.GitLab
{
    public class GitLabPullRequestClient : GitLabGitClient, IGitVendorPullRequestClient
    {
        readonly GitLabClient gitLabClient;

        public GitLabPullRequestClient(GitLabClient gitLabClient, IHttpsGitConnection repositoryConnection, Uri baseUrl)
            : base(repositoryConnection.Uri.Value, baseUrl)
        {
            this.gitLabClient = gitLabClient;
        }

        public async Task<PullRequest> CreatePullRequest(string pullRequestTitle,
                                                         string body,
                                                         GitBranchName sourceBranch,
                                                         GitBranchName destinationBranch,
                                                         CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            var mergeRequest = gitLabClient.GetMergeRequest(projectPath).Create(new MergeRequestCreate()
            {
                Title = pullRequestTitle,
                SourceBranch = sourceBranch.ToFriendlyName(),
                TargetBranch = destinationBranch.ToFriendlyName(),
                Description = body
            });
            return new PullRequest(mergeRequest.Title, mergeRequest.Iid, mergeRequest.WebUrl);
        }
    }
}

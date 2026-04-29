#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using NGitLab;
using NGitLab.Models;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.GitLab
{
    public class GitLabPullRequestClient : IGitVendorPullRequestClient
    {
        readonly GitLabClient gitLabClient;
        readonly Uri baseUrl;
        readonly string projectPath;

        public GitLabPullRequestClient(GitLabClient gitLabClient, IHttpsGitConnection repositoryConnection, Uri baseUrl)
        {
            this.gitLabClient = gitLabClient;
            this.baseUrl = baseUrl;
            
            var parts = repositoryConnection.Url.ParseAsHttpsUri().ExtractPropertiesFromUrlPath();
            projectPath = $"{parts[^2]}/{parts[^1]}";
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

        
        public string GenerateCommitUrl(string commit)
        {
            //return gitLabProviderApi.GetCommits(projectPath).GetCommit(commit).WebUrl;
            return $"{baseUrl.AbsoluteUri}/{projectPath}/-/commit/{commit}";
        }
    }
}

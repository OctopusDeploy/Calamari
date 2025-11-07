#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using NGitLab;
using NGitLab.Models;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    public class GitLabApiAdapter : IGitVendorApiAdapter
    {
        readonly GitLabClient gitLabClient;
        readonly Uri baseUrl;
        readonly string projectPath;

        public GitLabApiAdapter(GitLabClient gitLabClient, IRepositoryConnection repositoryConnection, Uri baseUrl)
        {
            this.gitLabClient = gitLabClient;
            this.baseUrl = baseUrl;
            
            var parts = repositoryConnection.Url.ExtractPropertiesFromUrlPath();
            projectPath = $"{parts[0]}/{parts[1]}";
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
                SourceBranch = sourceBranch.Value,
                TargetBranch = destinationBranch.Value,
                Description = body
            });
            return new PullRequest(mergeRequest.Title, mergeRequest.Id, mergeRequest.WebUrl);
        }

        
        public string GenerateCommitUrl(string commit)
        {
            //return gitLabProviderApi.GetCommits(projectPath).GetCommit(commit).WebUrl;
            return $"{baseUrl.AbsoluteUri}/{projectPath}/-/commit/{commit}";
        }
    }
}

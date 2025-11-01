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

        public GitLabApiAdapter(GitLabClient gitLabClient, IRepositoryConnection gitConnection, Uri baseUrl)
        {
            var pathParts = StripGitSuffix(gitConnection.Url.AbsolutePath).TrimStart('/').Split('/');
            projectPath = $"{pathParts[0]}/{pathParts[1]}";
            this.gitLabClient = gitLabClient;
            this.baseUrl = baseUrl;
        }
        
        public static string StripGitSuffix(string url)
        {
            const string gitExtension = ".git";
            if (url.EndsWith(gitExtension, StringComparison.OrdinalIgnoreCase))
                return url.Substring(0, url.Length - gitExtension.Length);

            return url;
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

using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git;
using Calamari.Common.Plumbing.Logging;
using Octokit;

namespace Calamari.ArgoCD.GitHub
{
    public interface IGitHubPullRequestCreator
    {
        Task<(int PullRequestNumber, string PullRequestUrl)> CreatePullRequest(ILog log,
                                                                               IGitConnection gitConnection,
                                                                               string prTitle, string body,
                                                                               GitBranchName sourceBranch,
                                                                               GitBranchName destinationBranch,
                                                                               CancellationToken cancellationToken);
    }

    public class GitHubPullRequestCreator : IGitHubPullRequestCreator
    {
        readonly IGitHubClientFactory gitHubClientFactory;
        public GitHubPullRequestCreator(IGitHubClientFactory gitHubClientFactory)
        {
            this.gitHubClientFactory = gitHubClientFactory;
        }

       public async Task<(int PullRequestNumber, string PullRequestUrl)> CreatePullRequest(ILog log,
                                                                                           IGitConnection gitConnection,
                                                                                           string prTitle,
                                                                                           string body,
                                                                                           GitBranchName sourceBranch,
                                                                                           GitBranchName destinationBranch,
                                                                                           CancellationToken cancellationToken)
        {
            log.Verbose("Attempting to use Git Credentials to talk to GitHub...");

            var (repoOwner, repoName) = GitHubRepositoryOwnerParser.ParseOwnerAndRepository(new Uri(gitConnection.Url));
            
            try
            {
                var client = gitHubClientFactory.CreateGitHubClient(gitConnection.Username, gitConnection.Password);
                log.Verbose($"Attempting to reach repository: owner={repoOwner} name={repoName}");
                var repo = await client.Repository.Get(repoOwner, repoName);

                var pr = await client.PullRequest.Create(repo.Id, new NewPullRequest(prTitle,
                    sourceBranch.Value,
                    destinationBranch.Value)
                {
                    Body = body
                });

                log.Info($"Pull Request [{pr.Title} (#{pr.Number})]({pr.HtmlUrl}) Created");

                return (PullRequestNumber: pr.Number, PullRequestUrl: pr.HtmlUrl);
            }
            catch (ApiException e)
            {
                log.Error($"GitHub Api Error: {e.Message}");
                log.Error(e.StackTrace);
                foreach (var err in e.ApiError.Errors)
                {
                    log.Error(err.Message);
                }

                throw new Exception("Pull Request Creation Failed", e);
            }
        }
    }
}

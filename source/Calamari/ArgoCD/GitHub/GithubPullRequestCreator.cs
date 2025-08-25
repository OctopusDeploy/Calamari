using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git;
using Calamari.Common.Plumbing.Logging;

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

       public async Task<(int PullRequestNumber, string PullRequestUrl)> CreatePullRequest(ILog log,
                                                                                           IGitConnection gitConnection,
                                                                                           string prTitle,
                                                                                           string body,
                                                                                           GitBranchName sourceBranch,
                                                                                           GitBranchName destinationBranch,
                                                                                           CancellationToken cancellationToken)
       {
           await Task.CompletedTask;
            log.Verbose("Attempting to use Git Credentials to talk to GitHub...");
            log.Warn("This is currently a NO-OP operations");
            return (-1, "not_a_real_url");
       }
    }
}

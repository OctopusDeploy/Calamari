#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Calamari.ArgoCD.Git.PullRequests
{
    public interface IGitVendorClient
    {
        string Name { get; }

        /// <summary>
        /// Returns a url that points to the web UI for the given commit.
        /// Note: This gets called _before_ the commit has been pushed to the remote.
        /// </summary>
        string GenerateCommitUrl(string commit);
    }

    // TODO: rename to IAuthenticatedGitVendorClient
    public interface IGitVendorPullRequestClient : IGitVendorClient
    {
        Task<PullRequest> CreatePullRequest(string pullRequestTitle,
                                            string body,
                                            GitBranchName sourceBranch,
                                            GitBranchName destinationBranch,
                                            CancellationToken cancellationToken);
    }
}

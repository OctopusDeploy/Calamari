#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    public interface IGitVendorApiAdapter
    {

        public Task<PullRequest> CreatePullRequest(string pullRequestTitle,
                                                   string body,
                                                   GitBranchName sourceBranch,
                                                   GitBranchName destinationBranch,
                                                   CancellationToken cancellationToken);

        /// <summary>
        /// Returns a url that points to the web UI for the given commit.
        /// Note: This gets called _before_ the commit has been pushed to the remote.
        /// </summary>
        /// <param name="commit"></param>
        /// <returns></returns>
        public string GenerateCommitUrl(string commit);
    }
}
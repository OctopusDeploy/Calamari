using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Git.PullRequests
{
    /// <summary>
    /// Using all registered <see cref="IGitVendorPullRequestClientFactory"/> instances, resolves the correct adapter based on which self-reports as being able to utilize the provided connection details.
    /// Returns the first matching adapter in the order provided in the constructor.
    /// </summary>
    public interface IGitVendorAgnosticPullRequestClientFactory: IGitVendorPullRequestClientFactory
    {
    }
    
    public class GitVendorPullRequestClientResolver: IGitVendorAgnosticPullRequestClientFactory
    {
        readonly IEnumerable<IGitVendorPullRequestClientFactory> gitHubClientFactory;

        public GitVendorPullRequestClientResolver(IEnumerable<IGitVendorPullRequestClientFactory> gitHubClientFactory)
        {
            this.gitHubClientFactory = gitHubClientFactory;
        }
 
        public IGitVendorPullRequestClient? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            foreach (var gitClientFactory in gitHubClientFactory)
            {
                var adapter = gitClientFactory.TryCreateGitVendorApiAdaptor(repositoryConnection);
                if(adapter != null)
                {
                    return adapter;
                }
            }

            // No Git Provider. Throw, log or do nothing
            return null;
        }
    }
}
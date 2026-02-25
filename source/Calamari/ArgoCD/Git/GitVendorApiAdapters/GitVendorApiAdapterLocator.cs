using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    /// <summary>
    /// Using all registered <see cref="IGitVendorApiAdapterFactory"/> instances, resolves the correct adapter based on which self-reports as being able to utilize the provided connection details.
    /// Returns the first matching adapter in the order provided in the constructor.
    /// </summary>
    public interface IGitVendorAgnosticApiAdapterFactory: IGitVendorApiAdapterFactory
    {
    }
    
    public class GitVendorAgnosticApiAdapterFactory: IGitVendorAgnosticApiAdapterFactory
    {
        readonly IEnumerable<IGitVendorApiAdapterFactory> gitHubClientFactory;

        public GitVendorAgnosticApiAdapterFactory(IEnumerable<IGitVendorApiAdapterFactory> gitHubClientFactory)
        {
            this.gitHubClientFactory = gitHubClientFactory;
        }

        public IGitVendorApiAdapter TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            var orderedRequests = gitHubClientFactory
                .OrderBy(f => typeof(IGitVendorApiAdapterSlowFactory).IsAssignableFrom(f.GetType()));
            foreach (var gitClientFactory in orderedRequests)
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
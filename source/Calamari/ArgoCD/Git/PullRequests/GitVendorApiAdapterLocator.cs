using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git.PullRequests
{
    /// <summary>
    /// Using all registered <see cref="IGitVendorPullRequestClientFactory"/> instances, resolves the correct adapter based on which self-reports as being able to utilize the provided connection details.
    /// Returns the first matching adapter in the order provided in the constructor.
    /// </summary>
    public interface IGitVendorPullRequestClientResolver
    {
        Task<IGitVendorPullRequestClient> TryResolve(IRepositoryConnection repositoryConnection, ILog log,
                                                     CancellationToken cancellationToken);
    }
    
    public class GitVendorPullRequestClientResolver: IGitVendorPullRequestClientResolver
    {
        readonly IEnumerable<IGitVendorPullRequestClientFactory> clientFactories;

        public GitVendorPullRequestClientResolver(IEnumerable<IGitVendorPullRequestClientFactory> clientFactories)
        {
            this.clientFactories = clientFactories;
        }
 
        public async Task<IGitVendorPullRequestClient?> TryResolve(IRepositoryConnection repositoryConnection, ILog log,
                                                                       CancellationToken cancellationToken)
        {
            //first try getting a handling factory by checking if it can be handled as a cloud hosted repo
            var handlingFactory = clientFactories.SingleOrDefault(f => f.CanHandleAsCloudHosted(repositoryConnection.Url));

            //if we still don't have a handling factory, try the self-hosted checks.
            if (handlingFactory is null)
            {
                foreach (var clientFactory in clientFactories)
                {
                    if (!await clientFactory.CanHandleAsSelfHosted(repositoryConnection.Url, cancellationToken))
                    {
                        continue;
                    }

                    handlingFactory = clientFactory;
                    break;
                }
            }

            log.Verbose($"Git vendor: {handlingFactory?.Name ?? "Unknown"}");

            return handlingFactory is not null
                ? await handlingFactory.Create(repositoryConnection, log, cancellationToken)
                : null;
        }
    }
}
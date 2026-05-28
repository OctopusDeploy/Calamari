using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git.PullRequests
{
    public interface IGitVendorPullRequestClientResolver
    {
        Task<IGitVendorPullRequestClient?> TryResolve(IHttpsGitConnection repositoryConnection, ILog log,
                                                      CancellationToken cancellationToken);
    }

    public class GitVendorPullRequestClientResolver: IGitVendorPullRequestClientResolver
    {
        readonly IEnumerable<IGitVendorPullRequestClientFactory> clientFactories;

        public GitVendorPullRequestClientResolver(IEnumerable<IGitVendorPullRequestClientFactory> clientFactories)
        {
            this.clientFactories = clientFactories;
        }

        public async Task<IGitVendorPullRequestClient?> TryResolve(IHttpsGitConnection repositoryConnection, ILog log,
                                                                   CancellationToken cancellationToken)
        {
            // Avoid using repositoryConnection.Uri here as we do not want to throw if we somehow got here without a
            // valid Uri - if we can gather confidence that this is impossible then we could remove this guard
            if (!Uri.TryCreate(repositoryConnection.Url, UriKind.Absolute, out var repositoryUri))
            {
                log.Verbose($"Could not load a Git vendor: URL is not a valid URI '{repositoryConnection.Url}'");
                return null;
            }

            //first try getting a handling factory by checking if it can be handled as a cloud hosted repo
            var handlingFactory = clientFactories.SingleOrDefault(f => f.CanHandleAsCloudHosted(repositoryUri));

            //if we still don't have a handling factory, try the self-hosted checks.
            if (handlingFactory is null)
            {
                foreach (var clientFactory in clientFactories)
                {
                    if (!await clientFactory.CanHandleAsSelfHosted(repositoryUri, cancellationToken))
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

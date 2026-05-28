#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git.PullRequests
{
    public interface IGitVendorClientResolver
    {
        /// <summary>
        /// Resolves the always-available vendor client (Name, commit URL generation).
        /// Returns null if no factory recognises the connection.
        /// </summary>
        Task<IGitVendorClient?> TryResolve(IGitConnection repositoryConnection, ILog log, CancellationToken cancellationToken);

        /// <summary>
        /// Resolves a pull-request-capable vendor client. Requires an <see cref="IHttpsGitConnection"/> because PR creation needs API credentials.
        /// Returns null if no factory recognises the connection.
        /// </summary>
        Task<IGitVendorPullRequestClient?> TryResolve(IHttpsGitConnection repositoryConnection, ILog log, CancellationToken cancellationToken);
    }

    public class GitVendorClientResolver : IGitVendorClientResolver
    {
        readonly IEnumerable<IGitVendorPullRequestClientFactory> clientFactories;

        public GitVendorClientResolver(IEnumerable<IGitVendorPullRequestClientFactory> clientFactories)
        {
            this.clientFactories = clientFactories;
        }

        public async Task<IGitVendorClient?> TryResolve(
            IGitConnection repositoryConnection,
            ILog log,
            CancellationToken cancellationToken)
        {
            var factory = await ResolveFactory(repositoryConnection.Url, log, cancellationToken);
            return factory?.Create(repositoryConnection);
        }

        public async Task<IGitVendorPullRequestClient?> TryResolve(
            IHttpsGitConnection repositoryConnection,
            ILog log,
            CancellationToken cancellationToken)
        {
            var factory = await ResolveFactory(repositoryConnection.Url, log, cancellationToken);
            return factory is null
                ? null
                : await factory.CreateForPullRequests(repositoryConnection, log, cancellationToken);
        }

        async Task<IGitVendorPullRequestClientFactory?> ResolveFactory(string url, ILog log, CancellationToken cancellationToken)
        {
            // Avoid using IHttpsGitConnection.Uri here as we do not want to throw if we somehow got here without a
            // valid Uri — if we can gather confidence that this is impossible then we could remove this guard.
            if (!Uri.TryCreate(url, UriKind.Absolute, out var repositoryUri))
            {
                log.Verbose($"Could not load a Git vendor: URL is not a valid URI '{url}'");
                return null;
            }

            var cloudHosted = clientFactories.SingleOrDefault(f => f.CanHandleAsCloudHosted(repositoryUri));
            if (cloudHosted is not null)
            {
                log.Verbose($"Git vendor: {cloudHosted.Name}");
                return cloudHosted;
            }

            foreach (var clientFactory in clientFactories)
            {
                if (await clientFactory.CanHandleAsSelfHosted(repositoryUri, cancellationToken))
                {
                    log.Verbose($"Git vendor: {clientFactory.Name}");
                    return clientFactory;
                }
            }

            log.Verbose("Git vendor: Unknown");
            return null;
        }
    }
}

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git.PullRequests
{
    public interface IGitVendorPullRequestClientFactory
    {
        string Name { get; }

        /// <summary>
        /// Indicates if this client factory can handle this repository as a known cloud hosted vendor.
        /// This typically is a plain URL check for know cloud hosted domains (e.g. github.com)
        /// </summary>
        bool CanHandleAsCloudHosted(Uri repositoryUri);

        /// <summary>
        /// Indicates if this client factory can handle this repository as a self-hosted instance.
        /// THis typically includes more complicated and slower checks, such as HTTP request interrogation.
        /// </summary>
        async Task<bool> CanHandleAsSelfHosted(Uri repositoryUri, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return false;
        }

        /// <summary>
        /// Creates a client that supports the always-available vendor capabilities (Name, commit URL generation).
        /// Available for any <see cref="IGitConnection"/> — including SSH — because no authenticated API access is required.
        /// </summary>
        IGitVendorClient Create(IGitConnection repositoryConnection);

        /// <summary>
        /// Creates a client that additionally supports pull request creation.
        /// Requires an <see cref="IHttpsGitConnection"/> because PR creation calls the vendor's HTTP API with credentials.
        /// </summary>
        Task<IGitVendorPullRequestClient> CreateForPullRequests(IHttpsGitConnection repositoryConnection, ILog log, CancellationToken cancellationToken);
    }
}

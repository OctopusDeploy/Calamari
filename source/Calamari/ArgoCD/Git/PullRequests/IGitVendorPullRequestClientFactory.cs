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

        Task<IGitVendorPullRequestClient> Create(HttpsGitConnection repositoryConnection, ILog log, CancellationToken cancellationToken);
    }
}

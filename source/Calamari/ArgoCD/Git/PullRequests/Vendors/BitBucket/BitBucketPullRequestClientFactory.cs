using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.BitBucket
{
    public class BitBucketPullRequestClientFactory : IGitVendorPullRequestClientFactory
    {
        public string Name => BitBucketGitClient.VendorName;
        static Uri baseUrl = new Uri("https://bitbucket.org");

        public bool CanHandleAsCloudHosted(Uri repositoryUri)
            => repositoryUri.Host.Equals(baseUrl.Host, StringComparison.OrdinalIgnoreCase);

        public IGitVendorClient Create(IGitConnection repositoryConnection)
            => new BitBucketGitClient(repositoryConnection.ResolveUri(), baseUrl);

        public async Task<IGitVendorPullRequestClient> CreateForPullRequests(IHttpsGitConnection repositoryConnection, ILog log, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new BitBucketPullRequestClient(repositoryConnection, baseUrl);
        }
    }
}

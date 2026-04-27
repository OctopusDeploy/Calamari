using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.BitBucket
{
    public class BitBucketPullRequestClientFactory: IGitVendorPullRequestClientFactory
    {
        public string Name => "BitBucket";
        static Uri baseUrl = new Uri("https://bitbucket.org");
        
        public bool CanHandleAsCloudHosted(Uri repositoryUri)
        {
            return repositoryUri.Host.Equals(baseUrl.Host, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<IGitVendorPullRequestClient> Create(HttpsGitConnection repositoryConnection, ILog log, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new BitBucketPullRequestClient(repositoryConnection, baseUrl);
        }
    }
}
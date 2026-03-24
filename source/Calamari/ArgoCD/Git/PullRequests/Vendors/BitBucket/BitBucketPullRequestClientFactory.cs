using System;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.BitBucket
{
    public class BitBucketPullRequestClientFactory: IGitVendorPullRequestClientFactory
    {
        public IGitVendorPullRequestClient? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            if (repositoryConnection.Url.Host.Equals(baseUrl.Host, StringComparison.OrdinalIgnoreCase))
            {
                return new BitBucketPullRequestClient(repositoryConnection, baseUrl);
            }

            return null;
        }

        static Uri baseUrl = new Uri("https://bitbucket.org");
    }
}
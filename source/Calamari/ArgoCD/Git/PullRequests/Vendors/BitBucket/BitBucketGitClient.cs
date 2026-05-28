using System;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.BitBucket
{
    public class BitBucketGitClient : IGitVendorClient
    {
        public const string VendorName = "BitBucket";

        protected readonly Uri baseUrl;
        protected readonly string workspace;
        protected readonly string repositorySlug;

        public BitBucketGitClient(Uri repositoryUri, Uri baseUrl)
        {
            this.baseUrl = baseUrl;

            var parts = repositoryUri.ExtractPropertiesFromUrlPath();
            workspace = parts[0];
            repositorySlug = parts[1];
        }

        public string Name => VendorName;

        public string GenerateCommitUrl(string commit)
            => $"{baseUrl.AbsoluteUri}/{workspace}/{repositorySlug}/commits/{commit}";
    }
}

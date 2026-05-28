#nullable enable
using System;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.GitHub
{
    public class GitHubGitClient : IGitVendorClient
    {
        public const string VendorName = "GitHub";

        protected readonly Uri baseUrl;
        protected readonly string repoOwner;
        protected readonly string repoName;

        public GitHubGitClient(Uri repositoryUri, Uri baseUrl)
        {
            this.baseUrl = baseUrl;

            var parts = repositoryUri.ExtractPropertiesFromUrlPath();
            repoOwner = parts[0];
            repoName = parts[1];
        }

        public string Name => VendorName;

        public string GenerateCommitUrl(string commit)
            => $"{baseUrl.AbsoluteUri}/{repoOwner}/{repoName}/commit/{commit}";
    }
}

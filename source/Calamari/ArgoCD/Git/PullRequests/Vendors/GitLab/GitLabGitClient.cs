#nullable enable
using System;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.GitLab
{
    public class GitLabGitClient : IGitVendorClient
    {
        public const string VendorName = "GitLab";

        protected readonly Uri baseUrl;
        protected readonly string projectPath;

        public GitLabGitClient(Uri repositoryUri, Uri baseUrl)
        {
            this.baseUrl = baseUrl;

            var parts = repositoryUri.ExtractPropertiesFromUrlPath();
            projectPath = $"{parts[^2]}/{parts[^1]}";
        }

        public string Name => VendorName;

        public string GenerateCommitUrl(string commit)
            => $"{baseUrl.AbsoluteUri}/{projectPath}/-/commit/{commit}";
    }
}

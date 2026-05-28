#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using NGitLab;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.GitLab
{
    public class GitLabPullRequestClientFactory(SelfHostedGitLabInspector selfHostedGitLabInspector) : IGitVendorPullRequestClientFactory
    {
        const string CloudHost = "https://gitlab.com";

        public string Name => GitLabGitClient.VendorName;

        public bool CanHandleAsCloudHosted(Uri repositoryUri) => IsCloudHostedGitLab(repositoryUri);

        public async Task<bool> CanHandleAsSelfHosted(Uri repositoryUri, CancellationToken cancellationToken)
        {
            //otherwise inspect to see if it's a self-hosted gitlab instance
            return await selfHostedGitLabInspector.IsSelfHostedGitLabInstance(repositoryUri, cancellationToken);
        }

        public IGitVendorClient Create(IGitConnection repositoryConnection)
        {
            var repositoryUri = repositoryConnection.ResolveUri();
            return new GitLabGitClient(repositoryUri, ResolveBaseUrl(repositoryUri));
        }

        public async Task<IGitVendorPullRequestClient> CreateForPullRequests(IHttpsGitConnection repositoryConnection,
                                                                             ILog log,
                                                                             CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            var baseUrl = ResolveBaseUrl(repositoryConnection.Uri.Value);
            var apiClient = new GitLabClient(baseUrl.AbsoluteUri.TrimEnd('/'), repositoryConnection.Password);
            return new GitLabPullRequestClient(apiClient, repositoryConnection, baseUrl);
        }

        Uri ResolveBaseUrl(Uri repositoryUri)
        {
            var host = IsCloudHostedGitLab(repositoryUri)
                ? CloudHost
                : SelfHostedGitLabInspector.GetSelfHostedBaseRepositoryUrl(repositoryUri);
            return new Uri(host);
        }

        static bool IsCloudHostedGitLab(Uri repositoryUri)
            => repositoryUri.Host.Equals("gitlab.com", StringComparison.OrdinalIgnoreCase) ||
               repositoryUri.Host.EndsWith(".gitlab.com", StringComparison.OrdinalIgnoreCase);
    }
}

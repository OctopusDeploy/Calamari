#nullable enable
using System;
using NGitLab;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.GitLab
{
    public class GitLabPullRequestClientFactory : IGitVendorApiAdapterFactory
    {
        public bool CanInvokeWith(IRepositoryConnection repositoryConnection)
        {
            return repositoryConnection.Url.Host.Equals("gitlab.com", StringComparison.OrdinalIgnoreCase)
                   //Handle www.gitlab.com
                   || repositoryConnection.Url.Host.EndsWith(".gitlab.com", StringComparison.OrdinalIgnoreCase);
        }

        public IGitVendorApiAdapter? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            if (!CanInvokeWith(repositoryConnection))
            {
                return null;
            }

            var client = new GitLabClient(CloudPortalHost.AbsoluteUri, repositoryConnection.Password);
            return new GitLabPullRequestClient(client, repositoryConnection, CloudPortalHost);
        }

        readonly Uri CloudPortalHost = new Uri("https://gitlab.com");
    }
}
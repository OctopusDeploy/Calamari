#nullable enable
using System;
using NGitLab;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    public class GitLabApiAdapterFactory : IGitVendorApiAdapterFactory
    {
        public bool CanInvokeWith(IRepositoryConnection repositoryConnection)
        {
            return repositoryConnection.Url.Host.Equals("gitlab.com", StringComparison.OrdinalIgnoreCase)
                   //Handle www.gitlab.com
                   || repositoryConnection.Url.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase);
        }

        public IGitVendorApiAdapter? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            if (!CanInvokeWith(repositoryConnection))
            {
                return null;
            }

            var client = new GitLabClient(CloudPortalHost.AbsoluteUri, repositoryConnection.Password);
            return new GitLabApiAdapter(client, repositoryConnection, CloudPortalHost);
        }

        readonly Uri CloudPortalHost = new Uri("https://gitlab.com");
    }
}
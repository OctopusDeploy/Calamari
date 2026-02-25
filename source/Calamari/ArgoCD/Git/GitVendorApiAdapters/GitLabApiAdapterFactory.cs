#nullable enable
using System;
using NGitLab;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    
    /// <summary>
    /// GitLab instance that the user runs on their own infrastructure,
    /// as opposed to <see cref="GitLabApiAdapterFactory"/>
    /// </summary>
    public class GitLabSelfManagedApiAdapterFactory: IGitVendorApiAdapterSlowFactory   {
        public IGitVendorApiAdapter? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            try
            {
                var uri = repositoryConnection.Url.AbsoluteUri.TrimEnd('/');
                var expectedUri = uri.Substring(0, uri.LastIndexOf('/', uri.LastIndexOf('/') - 1));
                
                var client = new GitLabClient(expectedUri, repositoryConnection.Password);
                if (!string.IsNullOrEmpty(client.Version.Get().Revision))
                {
                    return new GitLabApiAdapter(client, repositoryConnection, new Uri(expectedUri));
                }
            }
            catch (Exception)
            {
                return null; 
            }
            return null; 
        }
    }
    
    public class GitLabApiAdapterFactory : IGitVendorApiAdapterFactory
    {
        bool CanInvokeWith(IRepositoryConnection repositoryConnection)
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

            var client = new GitLabClient(cloudPortalHost.AbsoluteUri, repositoryConnection.Password);
            return new GitLabApiAdapter(client, repositoryConnection, cloudPortalHost);
        }

        readonly Uri cloudPortalHost = new Uri("https://gitlab.com");
    }
}
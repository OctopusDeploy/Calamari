#nullable enable
using System;
using System.Threading;
using NGitLab;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters.GitLab
{
    public class GitLabApiAdapterFactory : IResolvingGitVendorApiAdapterFactory
    {
        readonly string CloudHost = "https://gitlab.com";
        
        readonly SelfHostedGitLabInspector selfHostedGitLabInspector;

        public GitLabApiAdapterFactory(SelfHostedGitLabInspector selfHostedGitLabInspector)
        {
            this.selfHostedGitLabInspector = selfHostedGitLabInspector;
        }

        public bool CanHandleAsCloudHosted(Uri repositoryUri)
        {
            return repositoryUri.Host.Equals("gitlab.com", StringComparison.OrdinalIgnoreCase)
                   //Handle www.gitlab.com
                   || repositoryUri.Host.EndsWith(".gitlab.com", StringComparison.OrdinalIgnoreCase);
        }
        
        public bool CanHandleAsSelfHosted(IRepositoryConnection repositoryConnection)
        {
            return selfHostedGitLabInspector.IsSelfHostedGitLabInstance(repositoryConnection.Url, CancellationToken.None).Result;
        }
        
        public IGitVendorApiAdapter Create(IRepositoryConnection repositoryConnection)
        {
            var host = CanHandleAsCloudHosted(repositoryConnection.Url)
                ? CloudHost
                : SelfHostedGitLabInspector.GetSelfHostedBaseRepositoryUrl(repositoryConnection.Url);
            
            var client = new GitLabClient(host, repositoryConnection.Password);
            
            return new GitLabApiAdapter(client, repositoryConnection, new Uri(host));
        }
    }
}
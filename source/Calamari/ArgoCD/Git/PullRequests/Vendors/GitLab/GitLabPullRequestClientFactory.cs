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
        
        public string Name => "GitLab";
        
        public bool CanHandleAsCloudHosted(Uri repositoryUri) => IsCloudHostedGitLab(repositoryUri);
        
        public async Task<bool> CanHandleAsSelfHosted(Uri repositoryUri, CancellationToken cancellationToken)
        {
            //otherwise inspect to see if it's a self-hosted gitlab instance
            return await selfHostedGitLabInspector.IsSelfHostedGitLabInstance(repositoryUri, cancellationToken);
        }

        public async Task<IGitVendorPullRequestClient> Create(IRepositoryConnection repositoryConnection, ILog taskLog,
                                                              CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            //if we aren't cloud hosted, we must be self-hosted 
            var host = CanHandleAsCloudHosted(repositoryConnection.Url)
                ? CloudHost
                : SelfHostedGitLabInspector.GetSelfHostedBaseRepositoryUrl(repositoryConnection.Url);

            var client = new GitLabClient(host, repositoryConnection.Password);
            return new GitLabPullRequestClient(client, repositoryConnection, new Uri(host));
        }

        static bool IsCloudHostedGitLab(Uri repositoryUri)
            => repositoryUri.Host.Equals("gitlab.com", StringComparison.OrdinalIgnoreCase) ||
               repositoryUri.Host.EndsWith(".gitlab.com", StringComparison.OrdinalIgnoreCase);
    }
}
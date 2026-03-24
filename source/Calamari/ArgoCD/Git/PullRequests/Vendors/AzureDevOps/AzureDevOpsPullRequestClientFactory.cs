using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.AzureDevOps
{
    public class AzureDevOpsPullRequestClientFactory : IGitVendorAgnosticPullRequestClientFactory
    {
        public string Name => "Azure DevOps";
        
        public bool CanHandleAsCloudHosted(Uri repositoryUri) =>
            repositoryUri.Host.Equals(AzureDevOpsPullRequestClient.CloudHost, StringComparison.OrdinalIgnoreCase);
        
        public async Task<IGitVendorPullRequestClient> Create(IRepositoryConnection repositoryConnection, ILog log, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new AzureDevOpsPullRequestClient(repositoryConnection);
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.AzureDevOps
{
    public class AzureDevOpsPullRequestClientFactory : IGitVendorPullRequestClientFactory
    {
        public string Name => AzureDevOpsGitClient.VendorName;

        public bool CanHandleAsCloudHosted(Uri repositoryUri) => AzureDevOpsRepositoryUriParser.IsAzureDevOpsRepository(repositoryUri);

        public IGitVendorClient Create(IGitConnection repositoryConnection)
            => new AzureDevOpsGitClient(repositoryConnection.ResolveUri());

        public async Task<IGitVendorPullRequestClient> CreateForPullRequests(IHttpsGitConnection repositoryConnection, ILog log, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new AzureDevOpsPullRequestClient(repositoryConnection);
        }
    }
}

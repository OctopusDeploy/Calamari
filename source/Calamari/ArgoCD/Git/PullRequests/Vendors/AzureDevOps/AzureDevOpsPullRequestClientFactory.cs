using System;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.AzureDevOps
{
    public class AzureDevOpsPullRequestClientFactory : IGitVendorAgnosticApiAdapterFactory
    {
        public IGitVendorApiAdapter? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            return AzureDevOpsPullRequestClient.CanInvokeWith(repositoryConnection.Url) ? new AzureDevOpsPullRequestClient(repositoryConnection) : null;
        }
    }
}
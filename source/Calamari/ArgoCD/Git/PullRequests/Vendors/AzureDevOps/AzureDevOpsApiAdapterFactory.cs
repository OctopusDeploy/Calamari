using System;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.AzureDevOps
{
    public class AzureDevOpsApiAdapterFactory : IGitVendorAgnosticApiAdapterFactory
    {
        public IGitVendorApiAdapter? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            return AzureDevOpsApiAdapter.CanInvokeWith(repositoryConnection.Url) ? new AzureDevOpsApiAdapter(repositoryConnection) : null;
        }
    }
}
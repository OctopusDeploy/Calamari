using System;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    public class AzureDevOpsApiAdapterFactory : IGitVendorAgnosticApiAdapterFactory
    {
        public IGitVendorApiAdapter? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            return AzureDevOpsApiAdapter.CanInvokeWith(repositoryConnection.Url) ? new AzureDevOpsApiAdapter(repositoryConnection) : null;
        }
    }
}
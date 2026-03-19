using System;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters.AzureDevOps
{
    public class AzureDevOpsAgnosticApiAdapterFactory : IResolvingGitVendorApiAdapterFactory
    {
        public bool CanHandleAsCloudHosted(IRepositoryConnection repositoryConnection)
        {
            return AzureDevOpsApiAdapter.CanInvokeWith(repositoryConnection.Url);
        }
        public IGitVendorApiAdapter? Create(IRepositoryConnection repositoryConnection)
        {
            throw new NotImplementedException();
        }
    }
}
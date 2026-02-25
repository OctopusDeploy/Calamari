using System;
using System.Threading.Tasks;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    public class AzureDevOpsApiAdapterFactory : IGitVendorAgnosticApiAdapterFactory
    {
        public IGitVendorApiAdapter TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            return CanInvokeWith(repositoryConnection.Url) ? new AzureDevOpsApiAdapter(repositoryConnection) : null;
        }

        static bool CanInvokeWith(Uri uri)
        {
            return uri.Host.Equals(AzureDevOpsApiAdapter.Host);
        }
    }
}
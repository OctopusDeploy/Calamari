using System;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.AzureDevOps
{
    public class AzureDevOpsPullRequestClientFactory : IGitVendorAgnosticPullRequestClientFactory
    {
        public IGitVendorPullRequestClient? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            return AzureDevOpsPullRequestClient.CanInvokeWith(repositoryConnection.Url) ? new AzureDevOpsPullRequestClient(repositoryConnection) : null;
        }
    }
}
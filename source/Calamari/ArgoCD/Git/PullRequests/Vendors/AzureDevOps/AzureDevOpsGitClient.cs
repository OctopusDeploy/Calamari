using System;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.AzureDevOps;

public class AzureDevOpsGitClient : IGitVendorClient
{
    public const string VendorName = "AzureDevOps";
    protected const string CloudHost = "dev.azure.com";

    protected readonly Uri repositoryUri;

    public AzureDevOpsGitClient(Uri repositoryUri)
    {
        this.repositoryUri = repositoryUri;
    }

    public string Name => VendorName;

    public string GenerateCommitUrl(string commit)
    {
        var (organizationName, projectName, repositoryName) = AzureDevOpsRepositoryUriParser.Parse(repositoryUri);
        return $"https://{CloudHost}/{organizationName}/{projectName}/_git/{repositoryName}/commit/{commit}";
    }
}
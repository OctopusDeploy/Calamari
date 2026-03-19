namespace Calamari.ArgoCD.Git;

public interface IGitVendorApiResolver
{
    bool CanHandleAsCloudHosted(IRepositoryConnection repositoryConnection)
    {
        return false;
    }

    bool CanHandleAsSelfHosted(IRepositoryConnection repositoryConnection)
    {
        return false;
    }
}
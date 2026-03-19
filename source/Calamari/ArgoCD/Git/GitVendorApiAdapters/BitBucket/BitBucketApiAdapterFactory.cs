using System;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters.BitBucket
{
    public class BitBucketAgnosticApiAdapterFactory: IResolvingGitVendorApiAdapterFactory
    {
        static Uri baseUrl = new Uri("https://bitbucket.org");
        
        public bool CanHandleAsCloudHosted(IRepositoryConnection repositoryConnection)
        {
            return repositoryConnection.Url.Host.Equals(baseUrl.Host, StringComparison.OrdinalIgnoreCase);
        }
        
        public IGitVendorApiAdapter Create(IRepositoryConnection repositoryConnection)
        {
            return new BitBucketApiAdapter(repositoryConnection, baseUrl);
        }
    }
}
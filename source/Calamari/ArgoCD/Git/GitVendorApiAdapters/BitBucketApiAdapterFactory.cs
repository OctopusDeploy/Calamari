using System;
using Org.BouncyCastle.Tls;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    public class BitBucketApiAdapterFactory: IGitVendorApiAdapterFactory
    {
        public IGitVendorApiAdapter? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            if (repositoryConnection.Url.Host.Equals(baseUrl.Host, StringComparison.OrdinalIgnoreCase))
            {
                return new BitBucketApiAdapter(repositoryConnection, baseUrl);
            }

            return null;
        }

        static Uri baseUrl = new Uri("https://bitbucket.org");
    }
}
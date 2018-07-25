using System;
using Calamari.Integration.Certificates;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Compute;
using Microsoft.WindowsAzure.Management.Storage;
using Microsoft.WindowsAzure.Management.WebSites;

namespace Calamari.Azure.Accounts
{
    public static class AzureAccountExtensions
    {
        static SubscriptionCloudCredentials Credentials(this AzureAccount account, ICertificateStore certificateStore)
        {
            var certificate = certificateStore.GetOrAdd(account.CertificateThumbprint, account.CertificateBytes);
            return new CertificateCloudCredentials(account.SubscriptionNumber, certificate);
        }

        public static ComputeManagementClient CreateComputeManagementClient(this AzureAccount account, ICertificateStore certificateStore)
        {
            return string.IsNullOrWhiteSpace(account.ServiceManagementEndpointBaseUri)
                ? new ComputeManagementClient(account.Credentials(certificateStore))
                : new ComputeManagementClient(account.Credentials(certificateStore), new Uri(account.ServiceManagementEndpointBaseUri));
        }

        public static StorageManagementClient CreateStorageManagementClient(this AzureAccount account, ICertificateStore certificateStore)
        {
            return string.IsNullOrWhiteSpace(account.ServiceManagementEndpointBaseUri)
                ? new StorageManagementClient(account.Credentials(certificateStore))
                : new StorageManagementClient(account.Credentials(certificateStore), new Uri(account.ServiceManagementEndpointBaseUri));
        }

        public static WebSiteManagementClient CreateWebSiteManagementClient(this AzureAccount account, ICertificateStore certificateStore)
        {
            return string.IsNullOrWhiteSpace(account.ServiceManagementEndpointBaseUri)
                ? new WebSiteManagementClient(account.Credentials(certificateStore))
                : new WebSiteManagementClient(account.Credentials(certificateStore), new Uri(account.ServiceManagementEndpointBaseUri));
        }
    }
}
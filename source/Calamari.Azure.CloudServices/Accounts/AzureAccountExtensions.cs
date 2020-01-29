using System;
using Calamari.Integration.Certificates;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Compute;

namespace Calamari.Azure.CloudServices.Accounts
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
    }
}
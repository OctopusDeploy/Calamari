using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Compute;

namespace Calamari.AzureCloudService
{
    static class AzureAccountExtensions
    {
        static SubscriptionCloudCredentials Credentials(this AzureAccount account, X509Certificate2 certificate)
        {
            return new CertificateCloudCredentials(account.SubscriptionNumber, certificate);
        }

        public static ComputeManagementClient CreateComputeManagementClient(this AzureAccount account, X509Certificate2 certificate)
        {
            var httpClientProxy = new AuthHttpClientFactory().GetHttpClient();
            var credentials = account.Credentials(certificate);
            return string.IsNullOrWhiteSpace(account.ServiceManagementEndpointBaseUri)
                ? new ComputeManagementClient(credentials, httpClientProxy)
                : new ComputeManagementClient(credentials, new Uri(account.ServiceManagementEndpointBaseUri), httpClientProxy);
        }
    }
}
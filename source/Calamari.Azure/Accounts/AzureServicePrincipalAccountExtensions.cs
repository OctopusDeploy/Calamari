using System;
using Calamari.Azure.Integration.Security;
using Microsoft.Azure.Management.Compute;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Rest;

namespace Calamari.Azure.Accounts
{
    public static class AzureServicePrincipalAccountExtensions
    {
        public static ServiceClientCredentials Credentials(this AzureServicePrincipalAccount account)
        {
            return new TokenCredentials(GetAuthorizationToken(account));
        }

        public static ComputeManagementClient CreateComputeManagementClient(this AzureServicePrincipalAccount account)
        {
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ?
                new ComputeManagementClient(account.Credentials()) { SubscriptionId = account.SubscriptionNumber } :
                new ComputeManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), account.Credentials()) { SubscriptionId = account.SubscriptionNumber };
        }

        public static ResourceManagementClient CreateResourceManagementClient(this AzureServicePrincipalAccount account)
        {
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ?
                new ResourceManagementClient(account.Credentials()) { SubscriptionId = account.SubscriptionNumber } :
                new ResourceManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), account.Credentials()) { SubscriptionId = account.SubscriptionNumber };
        }

        public static StorageManagementClient CreateStorageManagementClient(this AzureServicePrincipalAccount account)
        {
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ?
                new StorageManagementClient(account.Credentials()) { SubscriptionId = account.SubscriptionNumber } :
                new StorageManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), account.Credentials()) { SubscriptionId = account.SubscriptionNumber };
        }

        public static WebSiteManagementClient CreateWebSiteManagementClient(this AzureServicePrincipalAccount account)
        {
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ?
                new WebSiteManagementClient(account.Credentials()) { SubscriptionId = account.SubscriptionNumber } :
                new WebSiteManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), account.Credentials()) { SubscriptionId = account.SubscriptionNumber };
        }

        static string GetAuthorizationToken(AzureServicePrincipalAccount account)
        {
            return ServicePrincipal.GetAuthorizationToken(account.TenantId, account.ClientId, account.Password,
                account.ResourceManagementEndpointBaseUri, account.ActiveDirectoryEndpointBaseUri);
        }
    }
}
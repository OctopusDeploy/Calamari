using System;
using Microsoft.Azure.Management.Compute;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure.Management.WebSites;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
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
            var adDirectory = "https://login.windows.net/";
            if (!string.IsNullOrWhiteSpace(account.ActiveDirectoryEndpointBaseUri))
            {
                adDirectory = account.ActiveDirectoryEndpointBaseUri;
            }
            var context = new AuthenticationContext(adDirectory + account.TenantId);

            var resourceManagementEndpointBaseUri = "https://management.core.windows.net/";
            if (!string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri))
            {
                resourceManagementEndpointBaseUri = account.ResourceManagementEndpointBaseUri;
            }
            var result = context.AcquireTokenAsync(resourceManagementEndpointBaseUri, new ClientCredential(account.ClientId, account.Password)).GetAwaiter().GetResult();
            return result.AccessToken;
        }
    }
}
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Rest;

namespace Calamari.AzureWebApp
{
    static class AzureServicePrincipalAccountExtensions
    {
        public static async Task<ServiceClientCredentials> Credentials(this AzureServicePrincipalAccount account)
        {
            return new TokenCredentials(await GetAuthorizationToken(account));
        }

        public static async Task<WebSiteManagementClient> CreateWebSiteManagementClient(this AzureServicePrincipalAccount account)
        {
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ?
                new WebSiteManagementClient(await account.Credentials(), AuthHttpClientFactory.ProxyClientHandler()) { SubscriptionId = account.SubscriptionNumber } :
                new WebSiteManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), await account.Credentials(), AuthHttpClientFactory.ProxyClientHandler()) { SubscriptionId = account.SubscriptionNumber };
        }

        static Task<string> GetAuthorizationToken(AzureServicePrincipalAccount account)
        {
            return ServicePrincipal.GetAuthorizationToken(account.TenantId, account.ClientId, account.Password,
                account.ResourceManagementEndpointBaseUri, account.ActiveDirectoryEndpointBaseUri);
        }
    }
}
using System;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Rest;

namespace Calamari.AzureWebApp
{
    static class AzureServicePrincipalAccountExtensions
    {
        public static ServiceClientCredentials Credentials(this AzureServicePrincipalAccount account)
        {
            return new TokenCredentials(GetAuthorizationToken(account));
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
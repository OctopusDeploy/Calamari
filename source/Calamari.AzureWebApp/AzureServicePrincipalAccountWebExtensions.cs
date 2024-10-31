using System;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Rest;

namespace Calamari.AzureWebApp
{
    static class AzureServicePrincipalAccountWebExtensions
    {
        public static async Task<WebSiteManagementClient> CreateWebSiteManagementClient(this AzureServicePrincipalAccount account)
        {
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ?
                new WebSiteManagementClient(await account.Credentials()) { SubscriptionId = account.SubscriptionNumber } :
                new WebSiteManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), await account.Credentials()) { SubscriptionId = account.SubscriptionNumber };
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Rest;
using Calamari.CloudAccounts;

namespace Calamari.AzureWebApp
{
    static class AzureOidcAccountWebExtensions
    {
        public static async Task<WebSiteManagementClient> CreateWebSiteManagementClient(this AzureOidcAccount account)
        {
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ?
                new WebSiteManagementClient(await account.Credentials(CancellationToken.None)) { SubscriptionId = account.SubscriptionNumber } :
                new WebSiteManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), await account.Credentials(CancellationToken.None)) { SubscriptionId = account.SubscriptionNumber };
        }
    }
}
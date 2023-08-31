using System;
using System.Threading.Tasks;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Rest;

namespace Calamari.AzureWebApp
{
    static class AzureOidcAccountExtensions
    {
        public static ServiceClientCredentials Credentials(this AzureOidcAccount account)
        {
            return new TokenCredentials(account.GetCredentials);
        }

        public static WebSiteManagementClient CreateWebSiteManagementClient(this AzureOidcAccount account)
        {
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ?
                new WebSiteManagementClient(account.Credentials()) { SubscriptionId = account.SubscriptionNumber } :
                new WebSiteManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), account.Credentials()) { SubscriptionId = account.SubscriptionNumber };
        }
    }
}
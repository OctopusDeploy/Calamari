using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Microsoft.Azure.Management.WebSites;

namespace Calamari.AzureWebApp
{
    public interface IAzureWebSiteManagementClientFactory
    {
        Task<WebSiteManagementClient> CreateWebSiteManagementClient(IAzureAccount account, CancellationToken cancellationToken);
    }
    
    public class AzureWebSiteManagementClientFactory : IAzureWebSiteManagementClientFactory
    {
        readonly IAzureAuthTokenService azureAuthTokenService;

        public AzureWebSiteManagementClientFactory(IAzureAuthTokenService azureAuthTokenService)
        {
            this.azureAuthTokenService = azureAuthTokenService;
        }

        public async Task<WebSiteManagementClient> CreateWebSiteManagementClient(IAzureAccount account, CancellationToken cancellationToken)
        {
            var credentials = await azureAuthTokenService.GetCredentials(account, cancellationToken);
            
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ?
                new WebSiteManagementClient(credentials) { SubscriptionId = account.SubscriptionNumber } :
                new WebSiteManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), credentials) { SubscriptionId = account.SubscriptionNumber };
        }
    }
}
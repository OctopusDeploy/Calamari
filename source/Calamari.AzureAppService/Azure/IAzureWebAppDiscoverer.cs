using System;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureAppService.Behaviors;
using Calamari.CloudAccounts;

namespace Calamari.AzureAppService.Azure
{
    /// <summary>
    /// Enumerates the Azure web apps and slots visible to an account. This is the single point at
    /// which target discovery talks to Azure; mocking it lets the tag-matching and target-creation
    /// logic in <see cref="Behaviors.TargetDiscoveryBehaviour" /> be tested without a real Azure connection.
    /// </summary>
    public interface IAzureWebAppDiscoverer
    {
        Task<AzureResource[]> DiscoverWebAppsAndSlots(IAzureAccount account);
    }

    public class AzureWebAppDiscoverer : IAzureWebAppDiscoverer
    {
        // These values are well-known resource types in Azure's API.
        // The format is {resource-provider}/{resource-type}
        // WebAppType refers to Azure Web Apps, Azure Functions Apps and Azure App Services
        // while WebAppSlotsType refers to Slots of any of the above resources.
        // More info about Azure Resource Providers and Types here:
        // https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-providers-and-types
        const string WebAppSlotsType = "microsoft.web/sites/slots";
        const string WebAppType = "microsoft.web/sites";

        public Task<AzureResource[]> DiscoverWebAppsAndSlots(IAzureAccount account)
        {
            var armClient = account.CreateArmClient(retryOptions =>
            {
                retryOptions.MaxDelay = TimeSpan.FromSeconds(10);
                retryOptions.MaxRetries = 5;
            });

            return armClient.GetResourcesByType(WebAppType, WebAppSlotsType);
        }
    }
}

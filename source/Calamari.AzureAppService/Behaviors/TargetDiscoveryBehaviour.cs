using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.ServiceMessages;
using Newtonsoft.Json;

#nullable enable
namespace Calamari.AzureAppService.Behaviors
{
    public class TargetDiscoveryBehaviour : IDeployBehaviour
    {
        // These values are well-known resource types in Azure's API.
        // The format is {resource-provider}/{resource-type}
        // WebAppType refers to Azure Web Apps, Azure Functions Apps and Azure App Services
        // while WebAppSlotsType refers to Slots of any of the above resources.
        // More info about Azure Resource Providers and Types here:
        // https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-providers-and-types
        private const string WebAppSlotsType = "microsoft.web/sites/slots";
        private const string WebAppType = "microsoft.web/sites";

        private ILog Log { get; }

        public TargetDiscoveryBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment runningDeployment)
        {
            const string contextVariableName = "Octopus.TargetDiscovery.Context";
            var json = runningDeployment.Variables.Get(contextVariableName);
            if (string.IsNullOrEmpty(json))
            {
                Log.Warn($"Could not find target discovery context in variable {contextVariableName}.");
                Log.Warn("Aborting target discovery.");
                return;
            }

            if (!TryGetAuthenticationMethod(json!, contextVariableName, out string? authenticationMethod))
                return;

            TargetDiscoveryContext<AccountAuthenticationDetails<IAzureAccount>>? targetDiscoveryContext = authenticationMethod == "AzureOidc"
                ? GetTargetDiscoveryContext<AzureOidcAccount>(json!)
                : GetTargetDiscoveryContext<AzureServicePrincipalAccount>(json!);
            
            if (targetDiscoveryContext?.Authentication == null || targetDiscoveryContext.Scope == null)
            {
                Log.Warn("Aborting target discovery.");
                return;
            }
            
            var account = targetDiscoveryContext.Authentication.AccountDetails;
            Log.Verbose("Looking for Azure web apps using:");
            Log.Verbose($"  Subscription ID: {account.SubscriptionNumber}");
            Log.Verbose($"  Tenant ID: {account.TenantId}");
            Log.Verbose($"  Client ID: {account.ClientId}");
            var armClient = account.CreateArmClient(retryOptions =>
            {
                retryOptions.MaxDelay = TimeSpan.FromSeconds(10);
                retryOptions.MaxRetries = 5;
            });
            try
            {
                var resources = await armClient.GetResourcesByType(WebAppType, WebAppSlotsType);
                var discoveredTargetCount = 0;
                Log.Verbose($"Found {resources.Length} candidate web app resources.");
                foreach (var resource in resources)
                {
                    var tagValues = resource.Tags;

                    if (tagValues == null)
                        continue;

                    var tags = AzureWebAppTagHelper.GetOctopusTags(tagValues);
                    var matchResult = targetDiscoveryContext.Scope.Match(tags);
                    if (matchResult.IsSuccess)
                    {
                        discoveredTargetCount++;
                        Log.Info($"Discovered matching web app resource: {resource.Name}");
                        WriteTargetCreationServiceMessage(
                            resource, targetDiscoveryContext, matchResult);
                    }
                    else
                    {
                        Log.Verbose($"Web app {resource.Name} does not match target requirements:");
                        foreach (var reason in matchResult.FailureReasons)
                        {
                            Log.Verbose($"- {reason}");
                        }
                    }
                }

                if (discoveredTargetCount > 0)
                {
                    Log.Info($"{discoveredTargetCount} target{(discoveredTargetCount > 1 ? "s" : "")} found.");
                }
                else
                {
                    Log.Warn("Could not find any Azure web app targets.");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Error connecting to Azure to look for web apps:");
                Log.Warn(ex.Message);
                Log.Warn("Aborting target discovery.");
            }
        }

        bool TryGetAuthenticationMethod(string json, string contextVariableName, out string? authenticationMethod)
        {
            try
            {
                var jsonObj = JsonConvert.DeserializeObject<dynamic>(json);
                authenticationMethod = jsonObj!.authentication.authenticationMethod;
                return true;
            }
            catch (JsonException ex)
            {
                Log.Warn($"Could not read authentication method from target discovery context, {contextVariableName} is in wrong format: {ex.Message}");
                authenticationMethod = null;
                return false;
            }
        }

        void WriteTargetCreationServiceMessage(
            AzureResource resource,
            TargetDiscoveryContext<AccountAuthenticationDetails<IAzureAccount>> context,
            TargetMatchResult matchResult)
        {
            var resourceName = resource.Name;
            string? slotName = null;
            if (resource.IsSlot)
            {
                slotName = resource.SlotName;
                resourceName = resource.ParentName;
            }

            Log.WriteServiceMessage(
                TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(
                    resource.ResourceGroup,
                    resourceName,
                    context.Authentication!.AccountId,
                    matchResult.Role,
                    context.Scope!.WorkerPoolId,
                    slotName));
        }

        private TargetDiscoveryContext<AccountAuthenticationDetails<IAzureAccount>>? GetTargetDiscoveryContext<T>(
            string json) where T : IAzureAccount
        {
            const string contextVariableName = "Octopus.TargetDiscovery.Context";
            try
            {
                var context = JsonConvert
                    .DeserializeObject<TargetDiscoveryContext<AccountAuthenticationDetails<T>>>(json);
                if (context?.Authentication != null)
                {
                    var accountAuthentication = context.Authentication;
                    var accountDetails = new AccountAuthenticationDetails<IAzureAccount>(accountAuthentication.Type, accountAuthentication.AccountId, accountAuthentication.AccountDetails);
                    var targetDiscoveryContext = new TargetDiscoveryContext<AccountAuthenticationDetails<IAzureAccount>>(context.Scope, accountDetails);
                    return targetDiscoveryContext;
                }
            }
            catch (JsonException ex)
            {
                Log.Warn($"Target discovery context from variable {contextVariableName} is in wrong format: {ex.Message}");
                return null;
            }

            return null;
        }
    }

    public static class TargetDiscoveryHelpers
    {
        public static ServiceMessage CreateWebAppTargetCreationServiceMessage(string? resourceGroupName, string webAppName, string accountId, string role, string? workerPoolId, string? slotName)
        {
            var parameters = new Dictionary<string, string?> {
                    { "azureWebApp", webAppName },
                    { "name", $"azure-web-app/{resourceGroupName}/{webAppName}{(slotName == null ? "" : $"/{slotName}")}" },
                    { "azureWebAppSlot", slotName },
                    { "azureResourceGroupName", resourceGroupName },
                    { "octopusAccountIdOrName", accountId },
                    { "octopusRoles", role },
                    { "updateIfExisting", "True" },
                    { "octopusDefaultWorkerPoolIdOrName", workerPoolId },
                    { "isDynamic", "True" }
                };

            return new ServiceMessage(
                "create-azurewebapptarget",
                parameters.Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value!));
        }

        public static async Task<AzureResource[]> GetResourcesByType(this ArmClient armClient, params string[] types)
        {
            var tenant = armClient.GetTenants().First();

            var typesToRetrieveClause = string.Join(" or ", types.Select(t => $"type == '{t}'"));
            var typeCondition = types.Any()
                ? $"| where { typesToRetrieveClause } |"
                : string.Empty;

            var query = new ResourceQueryContent(
                $"Resources {typeCondition} project name, type, tags, resourceGroup");

            var response = await tenant.GetResourcesAsync(query, CancellationToken.None);
            return JsonConvert.DeserializeObject<AzureResource[]>(response.Value.Data.ToString())!;
        }
    }
}
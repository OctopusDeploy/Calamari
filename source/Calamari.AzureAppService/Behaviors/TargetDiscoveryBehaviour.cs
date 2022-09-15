using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.ResourceManager.Models;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

#nullable enable
namespace Calamari.AzureAppService.Behaviors
{
    public class TargetDiscoveryBehaviour : IDeployBehaviour
    {
        private const int PageSize = 500;
        private ILog Log { get; }

        public TargetDiscoveryBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment runningDeployment)
        {
            var targetDiscoveryContext = GetTargetDiscoveryContext(runningDeployment.Variables);
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
            var armClient = account.CreateArmClient();

            try
            {
                var discoveredTargetCount = 0;
                var webApps = await armClient.GetResources(ArmClientExtensions.WebAppType, PageSize, CancellationToken.None);
                var slots = await armClient.GetResources(ArmClientExtensions.WebAppSlotsType, PageSize, CancellationToken.None);
                var resources = webApps.Concat(slots).ToList();
                Log.Verbose($"Found {resources.Count} candidate web app resources.");
                foreach (var resource in resources)
                {
                    var tagValues = resource.Data?.Tags;

                    if (tagValues == null)
                        continue;

                    var tags = AzureWebAppHelper.GetOctopusTags(new ReadOnlyDictionary<string, string>(tagValues));
                    var matchResult = targetDiscoveryContext.Scope.Match(tags);
                    if (matchResult.IsSuccess)
                    {
                        // Not all property values are given for each resource in the initial query. A second call
                        // is required for each resource to get addition required information such as the ResourceGroup.
                        var resourceProperties = await resource.GetResourceProperties(CancellationToken.None);

                        // resourceProperties will be null if the resource is removed between
                        // the initial query and attempting to get the properties.
                        if (resourceProperties == null)
                            continue;

                        discoveredTargetCount++;
                        Log.Info($"Discovered matching web app resource: {resource.Data!.Name}");
                        WriteTargetCreationServiceMessage(
                            resource.Data!, resourceProperties, targetDiscoveryContext, matchResult);
                    }
                    else
                    {
                        Log.Verbose($"Web app {resource.Data!.Name} does not match target requirements:");
                        foreach (var reason in matchResult.FailureReasons)
                        {
                            Log.Verbose($"- {reason}");
                        }
                    }
                }

                if (discoveredTargetCount > 0)
                {
                    Log.Info($"{discoveredTargetCount} targets found.");
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

        private void WriteTargetCreationServiceMessage(
            ResourceData resourceData,
            GenericResourceProperties resourceProperties,
            TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>> context,
            TargetMatchResult matchResult)
        {
            var resourceName = resourceData.Name;
            string? slotName = null;
            if (resourceData.ResourceType.Type.Equals("sites/slots", StringComparison.InvariantCultureIgnoreCase))
            {
                var indexOfSlash = resourceName.LastIndexOf("/", StringComparison.InvariantCulture);
                if (indexOfSlash >= 0)
                {
                    slotName = resourceName[(indexOfSlash + 1)..];
                    resourceName = resourceName[..indexOfSlash];
                }
            }

            Log.WriteServiceMessage(
                TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(
                    resourceProperties.ResourceGroup,
                    resourceName,
                    context.Authentication!.AccountId,
                    matchResult.Role,
                    context.Scope!.WorkerPoolId,
                    slotName));
        }

        private TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>>? GetTargetDiscoveryContext(
            IVariables variables)
        {
            const string contextVariableName = "Octopus.TargetDiscovery.Context";
            var json = variables.Get(contextVariableName);
            if (json == null)
            {
                Log.Warn($"Could not find target discovery context in variable {contextVariableName}.");
                return null;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            try
            {
                return JsonSerializer
                    .Deserialize<TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>>>(
                        json, options);
            }
            catch (JsonException ex)
            {
                Log.Warn($"Target discovery context from variable {contextVariableName} is in wrong format: {ex.Message}");
                return null;
            }
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
    }
}
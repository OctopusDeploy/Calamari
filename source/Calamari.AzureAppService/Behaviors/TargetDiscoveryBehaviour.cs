using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.AzureAppService.Azure.Rest;
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
        private ILog Log { get; }

        public TargetDiscoveryBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment runningDeployment)
        {
            await Task.CompletedTask;
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
            var restClient = new AzureRestClient(() => new HttpClient());
            await restClient.Authorise(account, CancellationToken.None);

            try
            {
                var discoveredTargetCount = 0;
                var resources = (await restClient.GetResources(CancellationToken.None, AzureRestClient.WebAppType,
                    AzureRestClient.WebAppSlotsType)).ToList();
                Log.Verbose($"Found {resources.Count} candidate web app resources.");
                foreach (var resource in resources)
                {
                    if (resource.Tags == null)
                        continue;

                    var tags = AzureWebAppHelper.GetOctopusTags(resource.Tags);
                    var matchResult = targetDiscoveryContext.Scope.Match(tags);
                    if (matchResult.IsSuccess)
                    {
                        var detailedResource = await restClient.GetResourceDetails(resource.Id, CancellationToken.None);

                        discoveredTargetCount++;
                        Log.Info($"Discovered matching web app resource: {detailedResource.Name}");
                        WriteTargetCreationServiceMessage(
                            detailedResource, targetDiscoveryContext, matchResult);
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
            AzureResource resource,
            TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>> context,
            TargetMatchResult matchResult)
        {
            var resourceName = resource.Name;
            string? slotName = null;
            // As of version 2022-03-01 of the Resource Details endpoint, the slotName property returned from
            // Azure is always null, meaning we have to calculate it manually. The slot also does not contain
            // a property for the WebAppName. Fortunately the name of a slot resource is {webAppName}/{slotName}
            // so the two required elements can be extracted.
            if (resource.Type.Equals(AzureRestClient.WebAppSlotsType, StringComparison.InvariantCultureIgnoreCase))
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
                    resource.Properties.ResourceGroup,
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
        public static ServiceMessage CreateWebAppTargetCreationServiceMessage(string resourceGroupName, string webAppName, string accountId, string role, string? workerPoolId, string? slotName)
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
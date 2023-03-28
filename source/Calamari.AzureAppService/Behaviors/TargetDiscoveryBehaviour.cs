using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Resources;
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
        private const string WebAppSlotsType = "sites/slots";
        private const string WebAppType = "sites";
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
            var armClient = account.CreateArmClient(retryOptions =>
            {
                retryOptions.MaxDelay = TimeSpan.FromSeconds(10);
                retryOptions.MaxRetries = 5;
            });
            var restClient = new AzureRestClient(() => new HttpClient());
            await restClient.Authorise(account, CancellationToken.None);
            var subscription = await armClient.GetDefaultSubscriptionAsync(CancellationToken.None);
            try
            {
                var discoveredTargetCount = 0;
                var webApps = subscription.GetResources(WebAppType, PageSize, CancellationToken.None);
                var slots = subscription.GetResources(WebAppSlotsType, PageSize, CancellationToken.None);
                var resources = await webApps.Concat(slots).ToListAsync();
                var restResources = (await restClient.GetResources(CancellationToken.None, AzureRestClient.WebAppType,
                    AzureRestClient.WebAppSlotsType)).ToList();
                Log.Verbose($"Found {resources.Count} candidate web app resources.");
                foreach (var (resource, index) in resources.Select((r,i) => (r,i)))
                {
                    var restResource = restResources[index];
                    var res1 = resource;
                    var isTestWebApp = resource.Data?.Name.Contains("isaac") ?? false;
                    if (isTestWebApp)
                    {
                        Log.Verbose($"Resource {resource.Data?.Name} Tags:");
                        foreach (var tag in resource.Data?.Tags ?? new Dictionary<string, string>())
                        {
                            Log.Verbose($"Name: {tag.Key}, Value: {tag.Value}");
                        }
                        res1 = (await resource.GetAsync(CancellationToken.None)).Value;
                        Log.Verbose($"FROM REST CLIENT ({restResource.Name})");
                        foreach (var tag in restResource.Tags)
                        {
                            Log.Verbose($"Name: {tag.Key}, Value: {tag.Value}");
                        }
                    }

                    var tagValues = res1.Data?.Tags;
                    if (isTestWebApp)
                    {
                        Log.Verbose("AFTER GET:");
                        foreach (var tag in tagValues ?? new Dictionary<string, string>())
                        {
                            Log.Verbose($"Name: {tag.Key}, Value: {tag.Value}");
                        }
                    }

                    if (tagValues == null)
                        continue;

                    var tags = AzureWebAppHelper.GetOctopusTags(new ReadOnlyDictionary<string, string>(tagValues));
                    var matchResult = targetDiscoveryContext.Scope.Match(tags);
                    if (matchResult.IsSuccess)
                    {
                        discoveredTargetCount++;
                        Log.Info($"Discovered matching web app resource: {resource.Data!.Name}");
                        WriteTargetCreationServiceMessage(
                            resource.Id, targetDiscoveryContext, matchResult);
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

        private void WriteTargetCreationServiceMessage(
            ResourceIdentifier identifier,
            TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>> context,
            TargetMatchResult matchResult)
        {
            var resourceName = identifier.Name;
            string? slotName = null;
            if (identifier.ResourceType.Type == WebAppSlotsType)
            {
                slotName = identifier.Name;
                resourceName = identifier.Parent!.Name;
            }

            Log.WriteServiceMessage(
                TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(
                    identifier.ResourceGroupName,
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

        public static AsyncPageable<GenericResource> GetResources(this SubscriptionResource subscription,
            string resourceType, int pageSize, CancellationToken cancellationToken)
        {
            return subscription.GetGenericResourcesAsync($"resourceType eq 'Microsoft.web/{resourceType}'",
                top: pageSize, cancellationToken: cancellationToken);
        }
    }
}
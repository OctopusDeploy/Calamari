using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Calamari.Azure;
////using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.AppService.Fluent;

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
            if (targetDiscoveryContext == null)
            {
                Log.Warn("Aborting target discovery.");
                return;
            }
            var account = targetDiscoveryContext.Authentication.AccountDetails;
            Log.Verbose($"Looking for Azure web apps using:");
            Log.Verbose($"  Subscription ID: {account.SubscriptionNumber}");
            Log.Verbose($"  Tenant ID: {account.TenantId}");
            Log.Verbose($"  Client ID: {account.ClientId}");
            var azureClient = account.CreateAzureClient();

            try
            {
                var discoveredTargetCount = 0;
                var webApps = azureClient.WebApps.List();
                Log.Verbose($"Found {webApps.Count()} candidate web apps.");
                foreach (var webApp in webApps)
                {
                    var tags = AzureWebAppHelper.GetOctopusTags(webApp.Tags);
                    var matchResult = targetDiscoveryContext.Scope.Match(tags);
                    if (matchResult.IsSuccess)
                    {
                        discoveredTargetCount++;
                        Log.Info($"Discovered matching web app: {webApp.Name}");
                        WriteTargetCreationServiceMessage(
                            webApp, targetDiscoveryContext, matchResult);
                    }
                    else
                    {
                        Log.Verbose($"Web app {webApp.Name} does not match target requirements:");
                        foreach (var reason in matchResult.FailureReasons)
                        {
                            Log.Verbose($"- {reason}");
                        }
                    }

                    var deploymentSlots = webApp.DeploymentSlots.List();

                    foreach (var slot in deploymentSlots)
                    {
                        var deploymentSlotTags = AzureWebAppHelper.GetOctopusTags(slot.Tags);
                        var deploymentSlotMatchResult = targetDiscoveryContext.Scope.Match(deploymentSlotTags);
                        if (deploymentSlotMatchResult.IsSuccess)
                        {
                            discoveredTargetCount++;
                            Log.Info($"Discovered matching deployment slot {slot.Name} in web app {webApp.Name}");
                            WriteDeploymentSlotTargetCreationServiceMessage(
                                webApp, slot, targetDiscoveryContext, deploymentSlotMatchResult);
                        }
                        else
                        {
                            Log.Verbose($"Deployment slot {slot.Name} in web app {webApp.Name} does not match target requirements:");
                            foreach (var reason in matchResult.FailureReasons)
                            {
                                Log.Verbose($"- {reason}");
                            }
                        }
                    }
                }

                if (discoveredTargetCount > 0)
                {
                    Log.Info($"{discoveredTargetCount} targets found.");
                }
                else
                {
                    Log.Warn($"Could not find any Azure web app targets.");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Error connecting to Azure to look for web apps:");
                Log.Warn(ex.Message);
                Log.Warn("Aborting target discovery.");
            }
        }

        private void WriteTargetCreationServiceMessage(
            IWebApp webApp,
            TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>> context,
            TargetMatchResult matchResult)
        {
            Log.WriteServiceMessage(
                TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(
                    webApp.ResourceGroupName,
                    webApp.Name,
                    context.Authentication.AccountId,
                    matchResult.Role,
                    context.Scope!.WorkerPoolId));
        }

        private void WriteDeploymentSlotTargetCreationServiceMessage(
            IWebApp webApp,
            IDeploymentSlot slot,
            TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>> context,
            TargetMatchResult matchResult)
        {
            Log.WriteServiceMessage(
                TargetDiscoveryHelpers.CreateWebAppDeploymentSlotTargetCreationServiceMessage(
                    webApp.ResourceGroupName, 
                    webApp.Name, 
                    slot.Name, 
                    context.Authentication.AccountId,
                    matchResult.Role,
                    context.Scope!.WorkerPoolId));
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
        public static ServiceMessage CreateWebAppTargetCreationServiceMessage(string resourceGroupName, string webAppName, string accountId, string role, string? workerPoolId)
        {
            var parameters = new Dictionary<string, string?> {
                    { "name", $"{resourceGroupName}/{webAppName}" },
                    { "azureWebApp", webAppName },
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

        public static ServiceMessage CreateWebAppDeploymentSlotTargetCreationServiceMessage(string resourceGroupName, string webAppName, string slotName, string accountId, string role, string? workerPoolId)
        {
            var parameters = new Dictionary<string, string?> {
                    { "name", $"{resourceGroupName}/{webAppName}/{slotName}" },
                    { "azureWebApp", webAppName },
                    { "azureResourceGroupName", resourceGroupName },
                    { "azureWebAppSlot", slotName },
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
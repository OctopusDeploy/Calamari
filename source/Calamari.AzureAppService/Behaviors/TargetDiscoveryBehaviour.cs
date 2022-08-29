using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Polly;

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
            Log.Verbose($"Looking for Azure web apps using:");
            Log.Verbose($"  Subscription ID: {account.SubscriptionNumber}");
            Log.Verbose($"  Tenant ID: {account.TenantId}");
            Log.Verbose($"  Client ID: {account.ClientId}");
            var azureClient = account.CreateAzureClient();

            try
            {
                var discoveredTargetCount = 0;
                var webApps = ListWebApps(azureClient);
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

                    Log.Verbose($"Looking for deployment slots in web app {webApp.Name}");

                    var deploymentSlots = ListDeploymentSlots(webApp);

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

        private IEnumerable<IWebApp> ListWebApps(IAzure azureClient)
        {
            var retryPolicy = CreateAzureQueryRetryPolicy(5, $"listing web apps");

            return retryPolicy.Execute(() =>
            {
                return azureClient.WebApps.List();
            });
        }

        private ISyncPolicy CreateAzureQueryRetryPolicy(int maxRetries, string description)
        {
            // Don't bother retrying for not found errors as we will only ever get the same response
            return Policy
                .Handle<DefaultErrorResponseException>()
                .WaitAndRetry(
                    maxRetries,
                    (retryAttempt, ex, context) =>
                    {
                        if (ex is DefaultErrorResponseException dex)
                        {
                            // Need to cast to an int here as net461 doesn't have TooManyRequests in the enum
                            if ((int)dex.Response.StatusCode == 429 && dex.Response.Headers.TryGetValue("Retry-After", out var retryAfter))
                            {
                                return TimeSpan.FromSeconds(int.Parse(retryAfter.First()));
                            }
                        }
                        // Not a specific throttling exception, use exponential backoff with a maximum wait time of 10 seconds
                        return TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, retryAttempt)));
                    },
                    (ex, delay, retryAttempt, context) =>
                    {
                        Log.Verbose($"An error has occurred {description}: {ex.Message}, retrying {retryAttempt} of {maxRetries} after {delay}");
                    });
        }

        private IEnumerable<IDeploymentSlot> ListDeploymentSlots(IWebApp webApp)
        {
            var retryPolicy = CreateAzureQueryRetryPolicy(5, $"listing deployment slots for web app {webApp.Name}");

            // We could get a 404 listing slots if the web app gets deleted 
            // after being found but before we can check it's slots, in this
            // case we'll log a message and continue
            var webAppNotFoundPolicy = Policy<IEnumerable<IDeploymentSlot>>
                .Handle<DefaultErrorResponseException>(dex => dex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                .Fallback(
                    fallbackValue: Enumerable.Empty<IDeploymentSlot>(),
                    onFallback: (exception, context) => Log.Verbose($"Could not list deployment slots for web app {webApp.Name} as it could no longer be found")
                );
            
            return retryPolicy.Wrap(webAppNotFoundPolicy).Execute(() =>
            {
                return webApp.DeploymentSlots.List();
            });
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
                    context.Authentication!.AccountId,
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
                    context.Authentication!.AccountId,
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
                    { "name", $"azure-web-app/{resourceGroupName}/{webAppName}" },
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
                    { "name", $"azure-web-app/{resourceGroupName}/{webAppName}/{slotName}" },
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
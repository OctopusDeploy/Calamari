using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Polly;
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
                var resources = (await GetResources(restClient, CancellationToken.None)).ToList();
                Log.Verbose($"Found {resources.Count} candidate web app resources.");
                foreach (var resource in resources)
                {
                    if (resource.Tags == null)
                        continue;

                    var tags = AzureWebAppHelper.GetOctopusTags(resource.Tags);
                    var matchResult = targetDiscoveryContext.Scope.Match(tags);
                    if (matchResult.IsSuccess)
                    {
                        // Not all property values are given for each resource in the initial query. A second call
                        // is required for each resource to get addition required information such as the ResourceGroup.
                        var detailedResource = await GetDetailedResource(restClient, resource, CancellationToken.None);

                        // When the resource is removed between getting the list of resources and getting the details
                        if (detailedResource == null)
                            continue;

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

        private async Task<IEnumerable<AzureResource>> GetResources(AzureRestClient restClient, CancellationToken cancellationToken)
        {
            var retryPolicy = CreateAzureQueryRetryPolicy(5, "listing web apps");

            return await retryPolicy.ExecuteAsync(async () =>
                await restClient.GetResources(cancellationToken,
                    AzureRestClient.WebAppType,
                    AzureRestClient.WebAppSlotsType));
        }

        /// <returns>The AzureDetailedResource or null if the resource can no longer be found.</returns>
        private async Task<AzureDetailedResource?> GetDetailedResource(AzureRestClient restClient, AzureResource resource, CancellationToken cancellationToken)
        {
            var retryPolicy = CreateAzureQueryRetryPolicy(5, $"getting details for resource {resource.Name}");

            // We could get a 404 listing slots if the web app gets deleted
            // after being found but before we can check it's slots, in this
            // case we'll log a message and continue
            var webAppNotFoundPolicy = Policy<AzureDetailedResource?>
                                       .Handle<AzureRestClientException>(dex => dex.Response.StatusCode == HttpStatusCode.NotFound)
                                       .FallbackAsync(fallbackValue: null,
                                           async (exception, context) =>
                                           {
                                               await Task.CompletedTask;
                                               Log.Verbose($"Could not get details for resource {resource.Name} as it could no longer be found");
                                           });

            return await retryPolicy.WrapAsync(webAppNotFoundPolicy)
                                    .ExecuteAsync(async () => await restClient.GetResourceDetails(resource, cancellationToken));
        }

        private IAsyncPolicy CreateAzureQueryRetryPolicy(int maxRetries, string description)
        {
            // Don't bother retrying for not found errors as we will only ever get the same response
            return Policy
                .Handle<AzureRestClientException>()
                .WaitAndRetryAsync(
                    maxRetries,
                    (retryAttempt, ex, context) =>
                    {
                        if (ex is AzureRestClientException arcex)
                        {
                            // Need to cast to an int here as net461 doesn't have TooManyRequests in the enum
                            if ((int)arcex.Response.StatusCode == 429 && arcex.Response.Headers.TryGetValues("Retry-After", out var retryAfter))
                            {
                                return TimeSpan.FromSeconds(int.Parse(retryAfter.First()));
                            }
                        }
                        // Not a specific throttling exception, use exponential backoff with a maximum wait time of 10 seconds
                        return TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, retryAttempt)));
                    },
                    async (ex, delay, retryAttempt, context) =>
                    {
                        await Task.CompletedTask;
                        Log.Verbose($"An error has occurred {description}: {ex.Message}, retrying {retryAttempt} of {maxRetries} after {delay}");
                    });
        }

        private void WriteTargetCreationServiceMessage(
            AzureDetailedResource resource,
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
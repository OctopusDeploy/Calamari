using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Calamari.Common.Plumbing.Logging;
using Polly;

namespace Calamari.AzureAppService.Azure
{
    public static class ArmClientExtensions
    {
        public const string WebAppSlotsType = "Microsoft.web/sites/slots";
        public const string WebAppType = "Microsoft.web/sites";

        public static async Task<IList<GenericResource>> GetResources(this ArmClient armClient, string resourceType, int pageSize, CancellationToken cancellationToken)
        {
            var retryPolicy = CreateAzureQueryRetryPolicy(5, "listing web apps");

            return await retryPolicy.ExecuteAsync(async () => await armClient.GetGenericResources(resourceType, pageSize, cancellationToken));
        }

        private static async Task<IList<GenericResource>> GetGenericResources(this ArmClient armClient,
            string resourceType, int pageSize, CancellationToken cancellationToken)
        {
            var subscription = await armClient.GetDefaultSubscriptionAsync(cancellationToken);
            var resourceCollection = subscription.GetGenericResourcesAsync($"resourceType eq '{resourceType}'",
                top: pageSize, cancellationToken: cancellationToken);

            return await resourceCollection.ToListAsync(cancellationToken);
        }

        /// <returns>The GenericResourceProperties or null if the resource can no longer be found.</returns>
        public static async Task<GenericResourceProperties> GetResourceProperties(this GenericResource resource,
            CancellationToken cancellationToken)
        {
            var retryPolicy = CreateAzureQueryRetryPolicy(5, $"getting details for resource {resource.Data.Name}");

            // We could get a 404 listing slots if the web app gets deleted
            // after being found but before we can check it's slots, in this
            // case we'll log a message and continue
            var webAppNotFoundPolicy = Policy<Response<GenericResource>>
                                       .HandleResult(r => r.GetRawResponse().Status == (int)HttpStatusCode.NotFound)
                                       .FallbackAsync(fallbackValue: null,
                                           async (exception, context) =>
                                           {
                                               await Task.CompletedTask;
                                               Log.Verbose($"Could not get details for resource {resource.Data.Name} as it could no longer be found");
                                           });

            var result = await retryPolicy.WrapAsync(webAppNotFoundPolicy)
                                          .ExecuteAsync(async () => await resource.GetAsync(cancellationToken));

            return result?.Value.Data.Properties.ToObjectFromJson<GenericResourceProperties>();
        }

        private static IAsyncPolicy CreateAzureQueryRetryPolicy(int maxRetries, string description)
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    maxRetries,
                    // Use exponential backoff with a maximum wait time of 10 seconds
                    (retryAttempt, ex, context) => TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, retryAttempt))),
                    async (ex, delay, retryAttempt, context) =>
                    {
                        await Task.CompletedTask;
                        Log.Verbose($"An error has occurred {description}: {ex.Message}, retrying {retryAttempt} of {maxRetries} after {delay}");
                    });
        }
    }

    public class GenericResourceProperties
    {
        [JsonPropertyName("resourceGroup")]
        public string ResourceGroup { get; set; }
    }
}
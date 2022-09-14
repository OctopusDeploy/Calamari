using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AzureAppService.Azure.Rest
{
    public class AzureRestClient
    {
        public const string WebAppSlotsType = "Microsoft.web/sites/slots";
        public const string WebAppType = "Microsoft.web/sites";

        private const string AuthorisationResource = "https://management.azure.com/";
        private const string EndpointVersionParameterName = "api-version";
        private const string GetResourcesEndpointVersion = "2022-09-01";
        private const string GetResourceDetailsEndpointVersion = "2022-03-01";
        private const string FilterParameterName = "$filter";

        private readonly Func<HttpClient> clientFactory;
        private AzureADToken azureAdToken;
        private string baseResourceManagementEndpoint;
        private string subscriptionNumber;

        public AzureRestClient(Func<HttpClient> clientFactory)
        {
            this.clientFactory = clientFactory;
        }

        public async Task Authorise(ServicePrincipalAccount account, CancellationToken cancellationToken)
        {
            var parameters = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", account.ClientId },
                { "client_secret", account.Password },
                { "resource", AuthorisationResource }
            };
            var requestBody = new FormUrlEncodedContent(parameters);
            var baseUri = account.ActiveDirectoryEndpointBaseUri;
            if (baseUri.IsNullOrEmpty())
                baseUri = DefaultVariables.ActiveDirectoryEndpoint;

            var requestUri = $"{baseUri}{account.TenantId}/oauth2/token";

            using var client = clientFactory();
            var response = await client.PostAsync(requestUri, requestBody, cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            azureAdToken = JsonConvert.DeserializeObject<AzureADToken>(responseContent);
            baseResourceManagementEndpoint = account.ResourceManagementEndpointBaseUri;
            if (baseResourceManagementEndpoint.IsNullOrEmpty())
                baseResourceManagementEndpoint = DefaultVariables.ResourceManagementEndpoint;
            subscriptionNumber = account.SubscriptionNumber;
        }

        public async Task<IEnumerable<AzureResource>> GetResources(CancellationToken cancellationToken,
            params string[] resourceTypes)
        {
            var parameters = new Dictionary<string, string>
            {
                { EndpointVersionParameterName, GetResourcesEndpointVersion }
            };

            if (TryCreateResourceTypeFilterParameter(resourceTypes, out var filterParameter))
            {
                parameters.Add(FilterParameterName, filterParameter);
            }

            var requestUri = $"{baseResourceManagementEndpoint}subscriptions/{subscriptionNumber}/resources";
            requestUri = QueryHelpers.AddQueryString(requestUri, parameters);

            using var client = GetAuthorisedHttpClient();

            var response = await client.GetAsync(requestUri, cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonConvert.DeserializeObject<AzureResourceCollection>(responseContent).Resources;
        }

        public async Task<AzureResource> GetResourceDetails(string id, CancellationToken cancellationToken)
        {
            var parameters = new Dictionary<string, string>
            {
                { EndpointVersionParameterName, GetResourceDetailsEndpointVersion }
            };

            var requestUri = QueryHelpers.AddQueryString(baseResourceManagementEndpoint + id, parameters);
            using var client = GetAuthorisedHttpClient();

            var response = await client.GetAsync(requestUri, cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var resource = JsonConvert.DeserializeObject<AzureResource>(responseContent);
            return resource;
        }

        private HttpClient GetAuthorisedHttpClient()
        {
            if (azureAdToken == null)
                throw new NotAuthorisedException("Unauthorised, please call Authorise first.");

            var client = clientFactory();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(azureAdToken.TokenType, azureAdToken.AccessToken);
            return client;
        }

        /// <remarks>
        /// For resource types:
        /// "Microsoft.web/sites", "Microsoft.web/sites/slots"
        /// The out parameter will be:
        /// "resourceType eq 'Microsoft.web/sites' or resourceType eq 'Microsoft.web/sites/slots'"
        /// </remarks>
        private static bool TryCreateResourceTypeFilterParameter(string[] resourceTypes, out string filterParameter)
        {
            if (!resourceTypes.Any())
            {
                filterParameter = null;
                return false;
            }

            filterParameter = string.Join(" or ", resourceTypes.Select(t => $"resourceType eq '{t}'"));
            return true;
        }
    }
}
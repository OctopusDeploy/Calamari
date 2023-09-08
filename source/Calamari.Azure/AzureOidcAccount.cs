using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Identity.Client;
using Microsoft.Rest;
using AzureEnvironmentEnum = Microsoft.Azure.Management.ResourceManager.Fluent.AzureEnvironment;

namespace Calamari.Azure
{
    public class AzureOidcAccount : IAzureAccount
    {        
        public AzureOidcAccount(
            string subscriptionNumber,
            string clientId,
            string tenantId,
            string jwtToken,
            string azureEnvironment,
            string resourceManagementEndpointBaseUri,
            string activeDirectoryEndpointBaseUri)
        {
            SubscriptionNumber = subscriptionNumber;
            ClientId = clientId;
            TenantId = tenantId;
            Jwt = jwtToken;
            AzureEnvironment = azureEnvironment;
            ResourceManagementEndpointBaseUri = resourceManagementEndpointBaseUri;
            ActiveDirectoryEndpointBaseUri = activeDirectoryEndpointBaseUri;
        }

        public string SubscriptionNumber { get;  }
        public string ClientId { get; }
        public string TenantId { get; }
        string Jwt { get; }
        public string AzureEnvironment { get; }
        public string ResourceManagementEndpointBaseUri { get; }
        public string ActiveDirectoryEndpointBaseUri { get; }
        public string GetCredentials() => Jwt;

        public IAzure CreateAzureClient()
        {
            var environment = string.IsNullOrEmpty(AzureEnvironment) || AzureEnvironment == "AzureCloud"
                ? AzureEnvironmentEnum.AzureGlobalCloud
                : AzureEnvironmentEnum.FromName(AzureEnvironment) ??
                throw new InvalidOperationException($"Unknown environment name {AzureEnvironment}");

            var accessToken = GetAuthorizationToken(TenantId, ClientId, Jwt, ResourceManagementEndpointBaseUri, ActiveDirectoryEndpointBaseUri).GetAwaiter().GetResult();
            var credentials = new AzureCredentials(
                                                   new TokenCredentials(accessToken),
                                                   new TokenCredentials(accessToken),
                                                   TenantId,
                                                   environment);

            // to ensure the Azure API uses the appropriate web proxy
            var client = new HttpClient(new HttpClientHandler {Proxy = WebRequest.DefaultWebProxy});

            return Microsoft.Azure.Management.Fluent.Azure.Configure()
                            .WithHttpClient(client)
                            .Authenticate(credentials)
                            .WithSubscription(SubscriptionNumber);
        }

        public static async Task<string> GetAuthorizationToken(string tenantId, string applicationId, string token, string managementEndPoint, string activeDirectoryEndPoint)
        {
            var authContext = GetOidcContextUri("https://login.microsoftonline.com/", tenantId);
            Log.Verbose($"Authentication Context: {authContext}");

            var app = ConfidentialClientApplicationBuilder.Create(applicationId)
                                                          .WithClientAssertion(token)
                                                          .WithAuthority(authContext)
                                                          .Build();

            var result = await app.AcquireTokenForClient(
                                                         new[] { $"https://management.azure.com/.default" })
                                  .WithTenantId(tenantId)
                                  .ExecuteAsync()
                                  .ConfigureAwait(false);
            return result.AccessToken;
        }

        static string GetOidcContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            if (!activeDirectoryEndPoint.EndsWith("/"))
            {
                return $"{activeDirectoryEndPoint}/{tenantId}/v2.0";
            }
            return $"{activeDirectoryEndPoint}{tenantId}/v2.0";
        }
    }
}

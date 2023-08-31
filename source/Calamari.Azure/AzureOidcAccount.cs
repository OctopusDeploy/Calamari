using System;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
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
            string accessToken,
            string azureEnvironment,
            string resourceManagementEndpointBaseUri,
            string activeDirectoryEndpointBaseUri)
        {
            SubscriptionNumber = subscriptionNumber;
            ClientId = clientId;
            TenantId = tenantId;
            AccessToken = accessToken;
            AzureEnvironment = azureEnvironment;
            ResourceManagementEndpointBaseUri = resourceManagementEndpointBaseUri;
            ActiveDirectoryEndpointBaseUri = activeDirectoryEndpointBaseUri;
        }

        public string SubscriptionNumber { get;  }
        public string ClientId { get; }
        public string TenantId { get; }
        string AccessToken { get; }
        public string AzureEnvironment { get; }
        public string ResourceManagementEndpointBaseUri { get; }
        public string ActiveDirectoryEndpointBaseUri { get; }
        public string GetCredentials() => AccessToken;

        public IAzure CreateAzureClient()
        {
            var environment = string.IsNullOrEmpty(AzureEnvironment) || AzureEnvironment == "AzureCloud"
                ? AzureEnvironmentEnum.AzureGlobalCloud
                : AzureEnvironmentEnum.FromName(AzureEnvironment) ??
                throw new InvalidOperationException($"Unknown environment name {AzureEnvironment}");

            var credentials = new AzureCredentials(
                                                   new TokenCredentials(AccessToken),
                                                   new TokenCredentials(AccessToken),
                                                   TenantId,
                                                   environment);

            // to ensure the Azure API uses the appropriate web proxy
            var client = new HttpClient(new HttpClientHandler {Proxy = WebRequest.DefaultWebProxy});

            return Microsoft.Azure.Management.Fluent.Azure.Configure()
                            .WithHttpClient(client)
                            .Authenticate(credentials)
                            .WithSubscription(SubscriptionNumber);
        }
    }
}

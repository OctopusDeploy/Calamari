using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using AzureEnvironmentEnum = Microsoft.Azure.Management.ResourceManager.Fluent.AzureEnvironment;

namespace Calamari.Azure
{
    public class ServicePrincipalAccount : IAzureAccount
    {        
        public ServicePrincipalAccount(
            string subscriptionNumber,
            string clientId,
            string tenantId,
            string password,
            string azureEnvironment,
            string resourceManagementEndpointBaseUri,
            string activeDirectoryEndpointBaseUri)
        {
            SubscriptionNumber = subscriptionNumber;
            ClientId = clientId;
            TenantId = tenantId;
            Password = password;
            AzureEnvironment = azureEnvironment;
            ResourceManagementEndpointBaseUri = resourceManagementEndpointBaseUri;
            ActiveDirectoryEndpointBaseUri = activeDirectoryEndpointBaseUri;
        }

        public string SubscriptionNumber { get;  }
        public string ClientId { get; }
        public string TenantId { get; }
        string Password { get; }
        public string AzureEnvironment { get; }
        public string ResourceManagementEndpointBaseUri { get; }
        public string ActiveDirectoryEndpointBaseUri { get; }
        
        string IAzureAccount.GetCredentials() => Password;

        public IAzure CreateAzureClient()
        {
            Task.FromResult(true);
            var environment = string.IsNullOrEmpty(AzureEnvironment) || AzureEnvironment == "AzureCloud"
                ? AzureEnvironmentEnum.AzureGlobalCloud
                : AzureEnvironmentEnum.FromName(AzureEnvironment) ??
                throw new InvalidOperationException($"Unknown environment name {AzureEnvironment}");

            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(ClientId,
                Password, TenantId, environment
            );

            // to ensure the Azure API uses the appropriate web proxy
            var client = new HttpClient(new HttpClientHandler {Proxy = WebRequest.DefaultWebProxy});

            return Microsoft.Azure.Management.Fluent.Azure.Configure()
                            .WithHttpClient(client)
                            .Authenticate(credentials)
                            .WithSubscription(SubscriptionNumber);
        }
    }
}

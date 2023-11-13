using System;
using System.Net.Http;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Newtonsoft.Json;
using AzureEnvironmentEnum = Microsoft.Azure.Management.ResourceManager.Fluent.AzureEnvironment;
using NetWebRequest = System.Net.WebRequest;

namespace Calamari.CloudAccounts
{
    public class AzureServicePrincipalAccount : IAzureAccount
    {
        [JsonConstructor]
        public AzureServicePrincipalAccount(
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

        public AzureServicePrincipalAccount(IVariables variables)
        {
            SubscriptionNumber = variables.Get(AccountVariables.SubscriptionId);
            ClientId = variables.Get(AccountVariables.ClientId);
            TenantId = variables.Get(AccountVariables.TenantId);
            Password = variables.Get(AccountVariables.Password);
            AzureEnvironment = variables.Get(AccountVariables.Environment);
            ResourceManagementEndpointBaseUri = variables.Get(AccountVariables.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);
            ActiveDirectoryEndpointBaseUri = variables.Get(AccountVariables.ActiveDirectoryEndPoint, DefaultVariables.ActiveDirectoryEndpoint);
        }

        public AccountType AccountType => AccountType.AzureServicePrincipal;
        public string GetCredentials => Password;
        public string SubscriptionNumber { get;  }
        public string ClientId { get; }
        public string TenantId { get; }
        // Public for JsonDeserialization
        public string Password { get; }
        public string AzureEnvironment { get; }
        public string ResourceManagementEndpointBaseUri { get; }
        public string ActiveDirectoryEndpointBaseUri { get; }

        public IAzure CreateAzureClient()
        {
            var environment = string.IsNullOrEmpty(AzureEnvironment) || AzureEnvironment == "AzureCloud"
                ? AzureEnvironmentEnum.AzureGlobalCloud
                : AzureEnvironmentEnum.FromName(AzureEnvironment) ??
                  throw new InvalidOperationException($"Unknown environment name {AzureEnvironment}");

            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(ClientId,
                                                                                      GetCredentials, TenantId, environment
                                                                                     );

            // to ensure the Azure API uses the appropriate web proxy
            var client = new HttpClient(new HttpClientHandler {Proxy = NetWebRequest.DefaultWebProxy});

            return Microsoft.Azure.Management.Fluent.Azure.Configure()
                                            .WithHttpClient(client)
                                            .Authenticate(credentials)
                                            .WithSubscription(SubscriptionNumber);
        }
    }
}

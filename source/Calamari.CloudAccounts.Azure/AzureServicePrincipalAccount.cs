using System;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.CloudAccounts.Azure
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
            SubscriptionNumber = variables.Get(AzureAccountVariables.SubscriptionId);
            ClientId = variables.Get(AzureAccountVariables.ClientId);
            TenantId = variables.Get(AzureAccountVariables.TenantId);
            Password = variables.Get(AzureAccountVariables.Password);
            AzureEnvironment = variables.Get(AzureAccountVariables.Environment);
            ResourceManagementEndpointBaseUri = variables.Get(AzureAccountVariables.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);
            ActiveDirectoryEndpointBaseUri = variables.Get(AzureAccountVariables.ActiveDirectoryEndPoint, DefaultVariables.ActiveDirectoryEndpoint);
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
    }
}
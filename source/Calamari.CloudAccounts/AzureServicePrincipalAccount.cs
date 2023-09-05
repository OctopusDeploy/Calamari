using System;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

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
        private string Password { get; }
        public string AzureEnvironment { get; }
        public string ResourceManagementEndpointBaseUri { get; }
        public string ActiveDirectoryEndpointBaseUri { get; }
    }
}

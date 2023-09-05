using System;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.CloudAccounts
{
    public class AzureOidcAccount : IAzureAccount
    {
        [JsonConstructor]
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
            AssertionToken = accessToken;
            AzureEnvironment = azureEnvironment;
            ResourceManagementEndpointBaseUri = resourceManagementEndpointBaseUri;
            ActiveDirectoryEndpointBaseUri = activeDirectoryEndpointBaseUri;
        }

        public AzureOidcAccount(IVariables variables)
        {
            SubscriptionNumber = variables.Get(AccountVariables.SubscriptionId);
            ClientId = variables.Get(AccountVariables.ClientId);
            TenantId = variables.Get(AccountVariables.TenantId);
            AssertionToken = variables.Get(AccountVariables.AssertionToken);
            AzureEnvironment = variables.Get(AccountVariables.Environment);
            ResourceManagementEndpointBaseUri = variables.Get(AccountVariables.ResourceManagementEndPoint, DefaultVariables.GraphManagementEndpoint);
            ActiveDirectoryEndpointBaseUri = variables.Get(AccountVariables.ActiveDirectoryEndPoint, DefaultVariables.OidcAuthContextUri);
        }

        public AccountType AccountType => AccountType.AzureOidc;
        public string GetCredentials => AssertionToken;
        public string SubscriptionNumber { get;  }
        public string ClientId { get; }
        public string TenantId { get; }
        private string AssertionToken { get; }
        public string AzureEnvironment { get; }
        public string ResourceManagementEndpointBaseUri { get; }
        public string ActiveDirectoryEndpointBaseUri { get; }
    }
}

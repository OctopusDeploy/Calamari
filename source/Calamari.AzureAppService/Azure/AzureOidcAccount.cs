using System;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.AzureAppService.Azure
{
    class AzureOidcAccount : IAzureAccount
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
            this.SubscriptionNumber = subscriptionNumber;
            this.ClientId = clientId;
            this.TenantId = tenantId;
            this.AccessToken = accessToken;
            this.AzureEnvironment = azureEnvironment;
            this.ResourceManagementEndpointBaseUri = resourceManagementEndpointBaseUri;
            this.ActiveDirectoryEndpointBaseUri = activeDirectoryEndpointBaseUri;
        }

        public AzureOidcAccount(IVariables variables)
        {
            this.SubscriptionNumber = variables.Get(AccountVariables.SubscriptionId);
            this.ClientId = variables.Get(AccountVariables.ClientId);
            this.TenantId = variables.Get(AccountVariables.TenantId);
            this.AccessToken = variables.Get(AccountVariables.AccessToken);
            this.AzureEnvironment = variables.Get(AccountVariables.Environment);
            this.ResourceManagementEndpointBaseUri = variables.Get(AccountVariables.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);
            this.ActiveDirectoryEndpointBaseUri = variables.Get(AccountVariables.ActiveDirectoryEndPoint, DefaultVariables.ActiveDirectoryEndpoint);
        }

        public AccountType AccountType => AccountType.AzureOidc;
        public string GetCredential => AccessToken;
        public string SubscriptionNumber { get;  }
        public string ClientId { get; }
        public string TenantId { get; }
        private string AccessToken { get; }
        public string AzureEnvironment { get; }
        public string ResourceManagementEndpointBaseUri { get; }
        public string ActiveDirectoryEndpointBaseUri { get; }
    }
}

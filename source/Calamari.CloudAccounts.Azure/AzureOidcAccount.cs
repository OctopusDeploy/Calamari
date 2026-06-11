using System;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.CloudAccounts.Azure
{
    public class AzureOidcAccount : IAzureAccount
    {
        [JsonConstructor]
        public AzureOidcAccount(
            string subscriptionNumber,
            string clientId,
            string tenantId,
            string jwt,
            string azureEnvironment,
            string resourceManagementEndpointBaseUri,
            string activeDirectoryEndpointBaseUri)
        {
            SubscriptionNumber = subscriptionNumber;
            ClientId = clientId;
            TenantId = tenantId;
            Jwt = jwt;
            AzureEnvironment = azureEnvironment;
            ResourceManagementEndpointBaseUri = resourceManagementEndpointBaseUri;
            ActiveDirectoryEndpointBaseUri = activeDirectoryEndpointBaseUri;
        }

        public AzureOidcAccount(IVariables variables)
        {
            SubscriptionNumber = variables.Get(AzureAccountVariables.SubscriptionId);
            ClientId = variables.Get(AzureAccountVariables.ClientId);
            TenantId = variables.Get(AzureAccountVariables.TenantId);
            Jwt = variables.Get(AzureAccountVariables.OpenIDJwt);
            AzureEnvironment = variables.Get(AzureAccountVariables.Environment);
            ResourceManagementEndpointBaseUri = variables.Get(AzureAccountVariables.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);
            ActiveDirectoryEndpointBaseUri = variables.Get(AzureAccountVariables.ActiveDirectoryEndPoint, DefaultVariables.OidcAuthContextUri);
        }

        public AccountType AccountType => AccountType.AzureOidc;
        public string GetCredentials => Jwt;
        public string SubscriptionNumber { get;  }
        public string ClientId { get; }
        public string TenantId { get; }
        public string Jwt { get; }
        public string AzureEnvironment { get; }
        public string ResourceManagementEndpointBaseUri { get; }
        public string ActiveDirectoryEndpointBaseUri { get; }

        internal static string GetDefaultScope(string environmentName)
        {
            switch (environmentName)
            {
                case "AzureChinaCloud":
                    return "https://management.chinacloudapi.cn/.default";
                case "AzureGermanCloud":
                    return "https://management.microsoftazure.de/.default";
                case "AzureUSGovernment":
                    return "https://management.usgovcloudapi.net/.default";
                case "AzureGlobalCloud":
                case "AzureCloud":
                default:
                    // The double slash is intentional for public cloud.
                    return "https://management.azure.com//.default";
            }
        }
    }
}
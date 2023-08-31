using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureWebApp
{
    class AzureOidcAccount : IAzureAccount
    {
        public AzureOidcAccount(IVariables variables)
        {
            SubscriptionNumber = variables.Get(AzureAccountVariables.SubscriptionId);
            ClientId = variables.Get(AzureAccountVariables.ClientId);
            TenantId = variables.Get(AzureAccountVariables.TenantId);
            AccessToken = variables.Get(AzureAccountVariables.AccessToken);

            AzureEnvironment = variables.Get(AzureAccountVariables.Environment);
            ResourceManagementEndpointBaseUri = variables.Get(AzureAccountVariables.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);
            ActiveDirectoryEndpointBaseUri = variables.Get(AzureAccountVariables.ActiveDirectoryEndPoint, DefaultVariables.ActiveDirectoryEndpoint);
        }

        public string SubscriptionNumber { get; set; }
        public string ClientId { get; set; }
        public string TenantId { get; set; }
        private string AccessToken { get; set; }

        public string AzureEnvironment { get; set; }
        public string ResourceManagementEndpointBaseUri { get; set; }
        public string ActiveDirectoryEndpointBaseUri { get; set; }

        public string GetCredentials => AccessToken;
        public AccountType AccountType => AccountType.AzureOidc;
    }
}
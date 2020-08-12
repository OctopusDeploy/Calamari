using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureWebApp
{
    class AzureServicePrincipalAccount
    {
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

        public string WhereHasMyStringGone { get; set; }
        public string SubscriptionNumber { get; set; }

        public string ClientId { get; set; }

        public string TenantId { get; set; }

        public string Password { get; set; }

        public string AzureEnvironment { get; set; }
        public string ResourceManagementEndpointBaseUri { get; set; }
        public string ActiveDirectoryEndpointBaseUri { get; set; }
    }
}
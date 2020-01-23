using Calamari.Azure.Integration;
using Calamari.Deployment;
using Octostache;

namespace Calamari.Azure.Accounts
{
    public class AzureServicePrincipalAccount
    {
        public AzureServicePrincipalAccount(VariableDictionary variables)
        {
            SubscriptionNumber = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            ClientId = variables.Get(SpecialVariables.Action.Azure.ClientId);
            TenantId = variables.Get(SpecialVariables.Action.Azure.TenantId);
            Password = variables.Get(SpecialVariables.Action.Azure.Password);

            AzureEnvironment = variables.Get(SpecialVariables.Action.Azure.Environment);
            ResourceManagementEndpointBaseUri = variables.Get(SpecialVariables.Action.Azure.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);
            ActiveDirectoryEndpointBaseUri = variables.Get(SpecialVariables.Action.Azure.ActiveDirectoryEndPoint, DefaultVariables.ActiveDirectoryEndpoint);
        }

        public string SubscriptionNumber { get; set; }

        public string ClientId { get; set; }

        public string TenantId { get; set; }

        public string Password { get; set; }

        public string AzureEnvironment { get; set; }
        public string ResourceManagementEndpointBaseUri { get; set; }
        public string ActiveDirectoryEndpointBaseUri { get; set; }
    }
}
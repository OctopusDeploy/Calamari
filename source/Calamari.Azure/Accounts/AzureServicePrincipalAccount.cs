using Calamari.Deployment;
using Calamari.Integration.Processes;

namespace Calamari.Azure.Accounts
{
    public class AzureServicePrincipalAccount : Account
    {
        public AzureServicePrincipalAccount(CalamariVariableDictionary variables)
        {
            SubscriptionNumber = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            ClientId = variables.Get(SpecialVariables.Action.Azure.ClientId);
            TenantId = variables.Get(SpecialVariables.Action.Azure.TenantId);
            Password = variables.Get(SpecialVariables.Action.Azure.Password);

            AzureEnvironment = variables.Get(SpecialVariables.Action.Azure.Environment);
            ResourceManagementEndpointBaseUri = variables.Get(SpecialVariables.Action.Azure.ResourceManagementEndPoint);
            ActiveDirectoryEndpointBaseUri = variables.Get(SpecialVariables.Action.Azure.ActiveDirectoryEndPoint);
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
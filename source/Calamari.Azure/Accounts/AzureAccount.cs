using Calamari.Deployment;
using Calamari.Integration.Processes;

namespace Calamari.Azure.Accounts
{
    public class AzureAccount : Account
    {
        public AzureAccount(CalamariVariableDictionary variables)
        {
            AzureEnvironment = variables.Get(SpecialVariables.Action.Azure.Environment);
            ServiceManagementEndpointBaseUri = variables.Get(SpecialVariables.Action.Azure.ServiceManagementEndPoint);
            ServiceManagementEndpointSuffix = variables.Get(SpecialVariables.Action.Azure.StorageEndPointSuffix);

            CertificateThumbprint = variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint);
            CertificateBytes = variables.Get(SpecialVariables.Action.Azure.CertificateBytes);
        }

        public string SubscriptionNumber { get; set; }
        public string CertificateThumbprint { get; set; }

        public string AzureEnvironment { get; set; }
        public string ServiceManagementEndpointBaseUri { get; set; }
        public string ServiceManagementEndpointSuffix { get; set; }

        public string CertificateBytes { get; set; }
    }
}
using System;
using Calamari.Azure.Integration;
using Calamari.Deployment;
using Octostache;

namespace Calamari.Azure.Accounts
{
    public class AzureAccount : Account
    {
        public AzureAccount(IVariables variables)
        {
            SubscriptionNumber = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            AzureEnvironment = variables.Get(SpecialVariables.Action.Azure.Environment);
            ServiceManagementEndpointBaseUri = variables.Get(SpecialVariables.Action.Azure.ServiceManagementEndPoint, DefaultVariables.ServiceManagementEndpoint);
            ServiceManagementEndpointSuffix = variables.Get(SpecialVariables.Action.Azure.StorageEndPointSuffix, DefaultVariables.StorageEndpointSuffix);

            CertificateThumbprint = variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint);
            CertificateBytes = Convert.FromBase64String(variables.Get(SpecialVariables.Action.Azure.CertificateBytes));
        }

        public string SubscriptionNumber { get; set; }
        public string CertificateThumbprint { get; set; }

        public string AzureEnvironment { get; set; }
        public string ServiceManagementEndpointBaseUri { get; set; }
        public string ServiceManagementEndpointSuffix { get; set; }

        public byte[] CertificateBytes { get; set; }
    }
}
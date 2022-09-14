using System;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureCloudService
{
    class AzureAccount
    {
        public AzureAccount(IVariables variables)
        {
            SubscriptionNumber = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            ServiceManagementEndpointBaseUri = variables.Get(SpecialVariables.Action.Azure.ServiceManagementEndPoint, DefaultVariables.ServiceManagementEndpoint);
            CertificateThumbprint = variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint);
            CertificateBytes = Convert.FromBase64String(variables.Get(SpecialVariables.Action.Azure.CertificateBytes));
        }

        public string SubscriptionNumber { get; }
        public string CertificateThumbprint { get; }
        public string ServiceManagementEndpointBaseUri { get; }
        public byte[] CertificateBytes { get; }
    }
}
#nullable disable
using Octopus.Server.MessageContracts;
using Octopus.Server.MessageContracts.Attributes;
using Octopus.Server.MessageContracts.Features.Machines;

namespace Sashimi.AzureServiceFabric.Endpoints
{
    public class ServiceFabricEndpointResource : EndpointResource
    {
#pragma warning disable 672
        public override CommunicationStyle CommunicationStyle => CommunicationStyle.AzureServiceFabricCluster;
#pragma warning restore 672

        [Trim]
        [Writeable]
        public string ConnectionEndpoint { get; set; }

        [Trim]
        [Writeable]
        public AzureServiceFabricSecurityMode SecurityMode { get; set; }

        [Trim]
        [Writeable]
        public string ServerCertThumbprint { get; set; }

        [Trim]
        [Writeable]
        public string ClientCertVariable { get; set; }

        [Trim]
        [Writeable]
        public string CertificateStoreLocation { get; set; }

        [Trim]
        [Writeable]
        public string CertificateStoreName { get; set; }

        [Trim]
        [Writeable]
        public AzureServiceFabricCredentialType AadCredentialType { get; set; }

        [Trim]
        [Writeable]
        public string AadClientCredentialSecret { get; set; }

        [Trim]
        [Writeable]
        public string AadUserCredentialUsername { get; set; }

        [Writeable]
        public SensitiveValue AadUserCredentialPassword { get; set; }

        [Trim]
        [Writeable]
        public string DefaultWorkerPoolId { get; set; }
    }
}

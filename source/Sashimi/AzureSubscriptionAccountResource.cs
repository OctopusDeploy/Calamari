#nullable disable
using System.ComponentModel.DataAnnotations;
using Octopus.Server.MessageContracts;
using Octopus.Server.MessageContracts.Attributes;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.AzureCloudService
{
    class AzureSubscriptionAccountResource : AccountDetailsResource
    {
        public AzureSubscriptionAccountResource()
        {
            CertificateBytes = new SensitiveValue();
        }

        public override AccountType AccountType => AccountTypes.AzureSubscriptionAccountType;

        [Trim]
        [Writeable]
        [Required(ErrorMessage = "Please provide an Azure subscription ID.")]
        public string SubscriptionNumber { get; set; }

        [Trim]
        [Writeable]
        public SensitiveValue CertificateBytes { get; set; }

        [Trim]
        [Writeable]
        public string CertificateThumbprint { get; set; }

        [Trim]
        [Writeable]
        public string AzureEnvironment { get; set; }

        [Trim]
        [Writeable]
        public string ServiceManagementEndpointBaseUri { get; set; }

        [Trim]
        [Writeable]
        public string ServiceManagementEndpointSuffix { get; set; }
    }
}
using Octopus.Server.MessageContracts.Features.Accounts;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.AzureCloudService
{
    class AzureCertificateRequiresPrivateKey : AccountStoreContributor
    {
        readonly CertificateEncoder certificateEncoder;

        public AzureCertificateRequiresPrivateKey(CertificateEncoder certificateEncoder)
        {
            this.certificateEncoder = certificateEncoder;
        }

        public override bool CanContribute(AccountResource resource)
        {
            return resource is AzureSubscriptionAccountResource;
        }

        public override ValidationResult ValidateResource(AccountResource accountResource)
        {
            var resource = (AzureSubscriptionAccountResource) accountResource;
            if (!string.IsNullOrWhiteSpace(resource.CertificateBytes.NewValue))
            {
                var cert = certificateEncoder.FromBase64String(resource.CertificateBytes.NewValue);

                if (!cert.HasPrivateKey)
                    return ValidationResult.Error("The X509 Certificate file lacks the private key. Please provide a file that includes the private key.");
            }

            return ValidationResult.Success;
        }
    }
}
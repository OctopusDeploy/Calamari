using Octopus.Server.MessageContracts.Features.Accounts;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.AzureCloudService
{
    class AzureCertificateThumbprintWillBeSet : AccountStoreContributor
    {
        readonly CertificateEncoder certificateEncoder;

        public AzureCertificateThumbprintWillBeSet(CertificateEncoder certificateEncoder)
        {
            this.certificateEncoder = certificateEncoder;
        }

        public override bool CanContribute(AccountResource resource)
        {
            return resource is AzureSubscriptionAccountResource;
        }

        public override void ModifyModel(AccountResource accountResource, AccountDetails accountModel, string name)
        {
            var resource = (AzureSubscriptionAccountResource)accountResource;

            if (string.IsNullOrWhiteSpace(resource.CertificateBytes.NewValue))
            {
                return;
            }

            var model = (AzureSubscriptionDetails)accountModel;
            var cert = certificateEncoder.FromBase64String(resource.CertificateBytes.NewValue);
            model.CertificateThumbprint = cert.Thumbprint;
        }
    }
}
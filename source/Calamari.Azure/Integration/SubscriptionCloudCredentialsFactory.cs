using Calamari.Shared.Certificates;
using Microsoft.WindowsAzure;

namespace Calamari.Azure.Integration
{
    public class SubscriptionCloudCredentialsFactory : ISubscriptionCloudCredentialsFactory
    {
        readonly ICertificateStore certificateStore;

        public SubscriptionCloudCredentialsFactory(ICertificateStore certificateStore)
        {
            this.certificateStore = certificateStore;
        }

        public SubscriptionCloudCredentials GetCredentials(string subscriptionId, string certificateThumbprint, string certificateBytes)
        {
            var certificate = certificateStore.GetOrAdd(certificateThumbprint, certificateBytes);
            return new CertificateCloudCredentials(subscriptionId, certificate);
        }
    }
}
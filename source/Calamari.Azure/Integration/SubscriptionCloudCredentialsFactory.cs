using System;
using Calamari.Shared;
using Calamari.Shared.Certificates;
using Microsoft.Azure;
using Octostache;

namespace Calamari.Azure.Integration
{
    public class SubscriptionCloudCredentialsFactory : ISubscriptionCloudCredentialsFactory
    {
        readonly ICertificateStore certificateStore;

        public SubscriptionCloudCredentialsFactory(ICertificateStore certificateStore)
        {
            this.certificateStore = certificateStore;
        }

        public SubscriptionCloudCredentials GetCredentials(VariableDictionary variables)
        {
            var subscriptionId = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            var certificateThumbprint = variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint);
            var certificateBytes = Convert.FromBase64String(variables.Get(SpecialVariables.Action.Azure.CertificateBytes));

            var certificate = certificateStore.GetOrAdd(certificateThumbprint, certificateBytes);
            return new CertificateCloudCredentials(subscriptionId, certificate);
        }
    }
}
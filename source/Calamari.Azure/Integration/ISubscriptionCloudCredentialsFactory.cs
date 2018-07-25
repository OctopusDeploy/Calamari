using Microsoft.Azure;

namespace Calamari.Azure.Integration
{
    public interface ISubscriptionCloudCredentialsFactory
    {
        SubscriptionCloudCredentials GetCredentials(string subscriptionId, string certificateThumbprint, string certificateBytes);
    }
}
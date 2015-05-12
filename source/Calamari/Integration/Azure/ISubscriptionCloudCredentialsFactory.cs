using Microsoft.WindowsAzure;

namespace Calamari.Integration.Azure
{
    public interface ISubscriptionCloudCredentialsFactory
    {
        SubscriptionCloudCredentials GetCredentials(string subscriptionId, string certificateThumbprint, string certificateBytes);
    }
}
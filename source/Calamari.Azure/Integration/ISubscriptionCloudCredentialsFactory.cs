using Microsoft.Azure;
using Octostache;

namespace Calamari.Azure.Integration
{
    public interface ISubscriptionCloudCredentialsFactory
    {
        SubscriptionCloudCredentials GetCredentials(IVariables variables);
    }
}
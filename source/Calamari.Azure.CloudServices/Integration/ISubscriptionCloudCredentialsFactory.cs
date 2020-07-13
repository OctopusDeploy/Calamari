using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure;
using Octostache;

namespace Calamari.Azure.CloudServices.Integration
{
    public interface ISubscriptionCloudCredentialsFactory
    {
        SubscriptionCloudCredentials GetCredentials(IVariables variables);
    }
}
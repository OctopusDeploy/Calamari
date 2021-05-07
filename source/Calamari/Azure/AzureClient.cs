using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Calamari.Azure
{
    internal static class AzureClient
    {
        public static IAzure CreateAzureClient(this ServicePrincipalAccount servicePrincipal)
        {
            return Microsoft.Azure.Management.Fluent.Azure.Configure()
                .Authenticate(
                    SdkContext.AzureCredentialsFactory.FromServicePrincipal(servicePrincipal.ClientId,
                        servicePrincipal.Password, servicePrincipal.TenantId,
                        !string.IsNullOrEmpty(servicePrincipal.AzureEnvironment)
                            ? AzureEnvironment.FromName(servicePrincipal.AzureEnvironment)
                            : AzureEnvironment.AzureGlobalCloud))
                .WithSubscription(servicePrincipal.SubscriptionNumber);
        }
    }
}
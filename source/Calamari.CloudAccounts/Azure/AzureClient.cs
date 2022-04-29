using System;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Calamari.CloudAccounts.Azure
{
    public static class AzureClient
    {
        public static IAzure CreateAzureClient(this ServicePrincipalAccount servicePrincipal)
        {
            var environment = new AzureKnownEnvironment(servicePrincipal.AzureEnvironment).AsAzureSDKEnvironment();
            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(servicePrincipal.ClientId,
                servicePrincipal.Password, servicePrincipal.TenantId, environment
            );

            // Note: This is a tactical fix to ensure this Sashimi uses the appropriate web proxy
            #pragma warning disable
            var client = new HttpClient(new HttpClientHandler {Proxy = WebRequest.DefaultWebProxy});

            return Microsoft.Azure.Management.Fluent.Azure.Configure()
                .WithHttpClient(client)
                .Authenticate(credentials)
                .WithSubscription(servicePrincipal.SubscriptionNumber);
        }
    }
}

using System.Net;
using System.Net.Http;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Calamari.Azure
{
    internal static class AzureClient
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

        /// <summary>
        /// Creates an ArmClient for the new Azure SDK, which replaces the older fluent libraries.
        /// We should migrate to this SDK once it stabilises.
        /// </summary>
        /// <param name="servicePrincipal">Service Principal Account to use when connecting to Azure</param>
        /// <returns></returns>
        public static ArmClient CreateArmClient(this ServicePrincipalAccount servicePrincipal)
        {
            var (armClientOptions, tokenCredentialOptions) = GetArmClientOptions(servicePrincipal);
            var credential = new ClientSecretCredential(servicePrincipal.TenantId, servicePrincipal.ClientId, servicePrincipal.Password, tokenCredentialOptions);
            return new ArmClient(credential, defaultSubscriptionId: servicePrincipal.SubscriptionNumber, armClientOptions);
        }

        public static (ArmClientOptions, TokenCredentialOptions) GetArmClientOptions(this ServicePrincipalAccount servicePrincipalAccount)
        {
            var azureKnownEnvironment = new AzureKnownEnvironment(servicePrincipalAccount.AzureEnvironment);

            // Configure a specific transport that will pick up the proxy settings set by Calamari
            var httpClientTransport = new HttpClientTransport(new HttpClientHandler { Proxy = WebRequest.DefaultWebProxy });

            // Specifically tell the new Azure SDK which authentication endpoint to use
            var authorityHost = azureKnownEnvironment.GetAzureAuthorityHost();
            var tokenCredentialOptions = new TokenCredentialOptions
            {
                Transport = httpClientTransport,
                AuthorityHost = authorityHost
            };

            // The new Azure SDK uses a different representation of Environments
            var armEnvironment = azureKnownEnvironment.AsAzureArmEnvironment();
            var armClientOptions = new ArmClientOptions()
            {
                Transport = httpClientTransport,
                Environment = armEnvironment
            };

            return (armClientOptions, tokenCredentialOptions);
        }
    }
}

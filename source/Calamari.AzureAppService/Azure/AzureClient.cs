using System;
using System.Net;
using System.Net.Http;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Calamari.AzureAppService.Azure
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
#pragma warning disable DE0003
            var client = new HttpClient(new HttpClientHandler {Proxy = WebRequest.DefaultWebProxy});
#pragma warning restore DE0003

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
        public static ArmClient CreateArmClient(this ServicePrincipalAccount servicePrincipal, Action<RetryOptions> retryOptionsSetter = null)
        {
            var (armClientOptions, tokenCredentialOptions) = GetArmClientOptions(servicePrincipal, retryOptionsSetter);
            var credential = new ClientSecretCredential(servicePrincipal.TenantId, servicePrincipal.ClientId, servicePrincipal.Password, tokenCredentialOptions);
            return new ArmClient(credential, defaultSubscriptionId: servicePrincipal.SubscriptionNumber, armClientOptions);
        }

        public static (ArmClientOptions, TokenCredentialOptions) GetArmClientOptions(this ServicePrincipalAccount servicePrincipalAccount, Action<RetryOptions> retryOptionsSetter = null)
        {
            var azureKnownEnvironment = new AzureKnownEnvironment(servicePrincipalAccount.AzureEnvironment);

            // Configure a specific transport that will pick up the proxy settings set by Calamari
#pragma warning disable DE0003
            var httpClientTransport = new HttpClientTransport(new HttpClientHandler { Proxy = WebRequest.DefaultWebProxy });
#pragma warning restore DE0003

            // Specifically tell the new Azure SDK which authentication endpoint to use
            var authorityHost = string.IsNullOrEmpty(servicePrincipalAccount.ActiveDirectoryEndpointBaseUri)
                ? azureKnownEnvironment.GetAzureAuthorityHost()
                // if the user has specified a custom authentication endpoint, use that value
                : new Uri(servicePrincipalAccount.ActiveDirectoryEndpointBaseUri);
            
            var tokenCredentialOptions = new TokenCredentialOptions
            {
                Transport = httpClientTransport,
                AuthorityHost = authorityHost
            };

            // The new Azure SDK uses a different representation of Environments
            var armEnvironment = string.IsNullOrEmpty(servicePrincipalAccount.ResourceManagementEndpointBaseUri)
                ? azureKnownEnvironment.AsAzureArmEnvironment()
                // if the user has specified a custom resource management endpoint, define a custom environment using that value
                : new ArmEnvironment(new Uri(servicePrincipalAccount.ResourceManagementEndpointBaseUri, UriKind.Absolute), servicePrincipalAccount.ResourceManagementEndpointBaseUri);
            
            var armClientOptions = new ArmClientOptions
            {
                Transport = httpClientTransport,
                Environment = armEnvironment
            };
            retryOptionsSetter?.Invoke(armClientOptions.Retry);
            
            // there is a bug in the slotconfignames call due to it not passing back an ID, so this is needed to fix that
            // see https://github.com/Azure/azure-sdk-for-net/issues/33384
            armClientOptions.AddPolicy(new SlotConfigNamesInvalidIdFilterPolicy(), HttpPipelinePosition.PerRetry);

            return (armClientOptions, tokenCredentialOptions);
        }
    }
}

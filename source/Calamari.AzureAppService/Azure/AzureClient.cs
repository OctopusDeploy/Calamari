using System;
using System.Net;
using System.Net.Http;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.ResourceManager;
using Calamari.CloudAccounts;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Rest;

namespace Calamari.AzureAppService.Azure
{
    internal static class AzureClient
    {
        public static IAzure CreateAzureClient(this IAzureAccount azureAccount)
        {
            var environment = new AzureKnownEnvironment(azureAccount.AzureEnvironment).AsAzureSDKEnvironment();

            AzureCredentials credentials;
            if (azureAccount.AccountType == AccountType.AzureServicePrincipal)
            {
                credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(azureAccount.ClientId,
                                                                              azureAccount.GetCredentials,
                                                                              azureAccount.TenantId,
                                                                              environment);
            }
            else
            {
                var accessToken = ((AzureOidcAccount)azureAccount).GetAuthorizationToken().GetAwaiter().GetResult();
                credentials = new AzureCredentials(new TokenCredentials(accessToken),
                                                new TokenCredentials(accessToken),
                                                azureAccount.TenantId,
                                                environment);
            }

            // Note: This is a tactical fix to ensure this Sashimi uses the appropriate web proxy
#pragma warning disable DE0003
            var client = new HttpClient(new HttpClientHandler { Proxy = WebRequest.DefaultWebProxy });
#pragma warning restore DE0003

            return Microsoft.Azure.Management.Fluent.Azure.Configure()
                            .WithHttpClient(client)
                            .Authenticate(credentials)
                            .WithSubscription(azureAccount.SubscriptionNumber);
        }

        /// <summary>
        /// Creates an ArmClient for the new Azure SDK, which replaces the older fluent libraries.
        /// We should migrate to this SDK once it stabilises.
        /// </summary>
        /// <param name="azureAccount">Account to use when connecting to Azure</param>
        /// <returns></returns>
        public static ArmClient CreateArmClient(this IAzureAccount azureAccount, Action<RetryOptions> retryOptionsSetter = null)
        {
            string clientSecret;
            clientSecret = azureAccount.AccountType == AccountType.AzureOidc
                ? ((AzureOidcAccount)azureAccount).GetAuthorizationToken().GetAwaiter().GetResult()
                : azureAccount.GetCredentials;
            
            var (armClientOptions, tokenCredentialOptions) = GetArmClientOptions(azureAccount, retryOptionsSetter);
            var credential = new ClientSecretCredential(azureAccount.TenantId, azureAccount.ClientId, clientSecret, tokenCredentialOptions);
            return new ArmClient(credential, defaultSubscriptionId: azureAccount.SubscriptionNumber, armClientOptions);
        }

        public static (ArmClientOptions, TokenCredentialOptions) GetArmClientOptions(this IAzureAccount azureAccount, Action<RetryOptions> retryOptionsSetter = null)
        {
            var azureKnownEnvironment = new AzureKnownEnvironment(azureAccount.AzureEnvironment);

            // Configure a specific transport that will pick up the proxy settings set by Calamari
#pragma warning disable DE0003
            var httpClientTransport = new HttpClientTransport(new HttpClientHandler { Proxy = WebRequest.DefaultWebProxy });
#pragma warning restore DE0003

            // Specifically tell the new Azure SDK which authentication endpoint to use
            var authorityHost = string.IsNullOrEmpty(azureAccount.ActiveDirectoryEndpointBaseUri)
                ? azureKnownEnvironment.GetAzureAuthorityHost()
                // if the user has specified a custom authentication endpoint, use that value
                : new Uri(azureAccount.ActiveDirectoryEndpointBaseUri);

            var tokenCredentialOptions = new TokenCredentialOptions
            {
                Transport = httpClientTransport,
                AuthorityHost = authorityHost
            };

            // The new Azure SDK uses a different representation of Environments
            var armEnvironment = string.IsNullOrEmpty(azureAccount.ResourceManagementEndpointBaseUri)
                ? azureKnownEnvironment.AsAzureArmEnvironment()
                // if the user has specified a custom resource management endpoint, define a custom environment using that value
                : new ArmEnvironment(new Uri(azureAccount.ResourceManagementEndpointBaseUri, UriKind.Absolute), azureAccount.ResourceManagementEndpointBaseUri);

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
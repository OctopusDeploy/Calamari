using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.ResourceManager;
using Calamari.CloudAccounts;

namespace Calamari.Azure
{
    public static class AzureClient
    {
        /// <summary>
        /// Creates an ArmClient for the new Azure SDK, which replaces the older fluent libraries.
        /// We should migrate to this SDK once it stabilises.
        /// </summary>
        /// <param name="azureAccount">Account to use when connecting to Azure</param>
        /// <returns></returns>
        public static ArmClient CreateArmClient(this IAzureAccount azureAccount, Action<RetryOptions> retryOptionsSetter = null)
        {
            if (azureAccount.AccountType == AccountType.AzureOidc)
            {
                var oidcAccount = (AzureOidcAccount)azureAccount;
                var (clientOptions, _) = GetArmClientOptions(azureAccount, retryOptionsSetter);
                var clientAssertionCreds = new ClientAssertionCredential(oidcAccount.TenantId, oidcAccount.ClientId, () => oidcAccount.GetCredentials);
                return new ArmClient(clientAssertionCreds, defaultSubscriptionId: azureAccount.SubscriptionNumber, clientOptions);
            }

            var clientSecret = azureAccount.GetCredentials;

            var (armClientOptions, tokenCredentialOptions) = GetArmClientOptions(azureAccount, retryOptionsSetter);
            var credential = new ClientSecretCredential(azureAccount.TenantId, azureAccount.ClientId, clientSecret, tokenCredentialOptions);
            return new ArmClient(credential, defaultSubscriptionId: azureAccount.SubscriptionNumber, armClientOptions);
        }

        public static (ArmClientOptions, TokenCredentialOptions) GetArmClientOptions(this IAzureAccount azureAccount, Action<RetryOptions> retryOptionsSetter = null)
        {
            var azureKnownEnvironment = new AzureKnownEnvironment(azureAccount.AzureEnvironment);

            // Configure the specific transport that will pick up the proxy settings set by Calamari
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
        
        public static async Task<string> GetAccessTokenAsync(this IAzureAccount azureAccount)
        {
           return azureAccount.AccountType == AccountType.AzureOidc
                ? await AzureOidcAccountExtensions.GetAuthorizationToken(azureAccount.TenantId,
                                                                         azureAccount.ClientId,
                                                                         azureAccount.GetCredentials,
                                                                         azureAccount.ResourceManagementEndpointBaseUri,
                                                                         azureAccount.ActiveDirectoryEndpointBaseUri,
                                                                         azureAccount.AzureEnvironment,
                                                                         CancellationToken.None)
                : await AzureServicePrincipalAccountExtensions.GetAuthorizationToken(azureAccount.TenantId,
                                                                                     azureAccount.ClientId,
                                                                                     azureAccount.GetCredentials,
                                                                                     azureAccount.ResourceManagementEndpointBaseUri,
                                                                                     azureAccount.ActiveDirectoryEndpointBaseUri);
        }
    }
}
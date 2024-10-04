using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Identity.Client;
using Newtonsoft.Json;

namespace Calamari.CloudAccounts
{
    public class AzureServicePrincipalAccount : IAzureAccount
    {
        [JsonConstructor]
        public AzureServicePrincipalAccount(
            string subscriptionNumber,
            string clientId,
            string tenantId,
            string password,
            string azureEnvironment,
            string resourceManagementEndpointBaseUri,
            string activeDirectoryEndpointBaseUri)
        {
            SubscriptionNumber = subscriptionNumber;
            ClientId = clientId;
            TenantId = tenantId;
            Password = password;
            AzureEnvironment = azureEnvironment;
            ResourceManagementEndpointBaseUri = resourceManagementEndpointBaseUri;
            ActiveDirectoryEndpointBaseUri = activeDirectoryEndpointBaseUri;
        }

        public AzureServicePrincipalAccount(IVariables variables)
        {
            SubscriptionNumber = variables.Get(AccountVariables.SubscriptionId);
            ClientId = variables.Get(AccountVariables.ClientId);
            TenantId = variables.Get(AccountVariables.TenantId);
            Password = variables.Get(AccountVariables.Password);
            AzureEnvironment = variables.Get(AccountVariables.Environment);
            ResourceManagementEndpointBaseUri = variables.Get(AccountVariables.ResourceManagementEndPoint, DefaultAccountEndpoints.ResourceManagementEndpoint);
            ActiveDirectoryEndpointBaseUri = variables.Get(AccountVariables.ActiveDirectoryEndPoint, DefaultAccountEndpoints.ActiveDirectoryEndpoint);
        }

        public AccountType AccountType => AccountType.AzureServicePrincipal;
        public string GetCredentials => Password;

        public string SubscriptionNumber { get; }
        public string ClientId { get; }

        public string TenantId { get; }

        // Public for JsonDeserialization
        public string Password { get; }
        public string AzureEnvironment { get; }
        public string ResourceManagementEndpointBaseUri { get; }
        public string ActiveDirectoryEndpointBaseUri { get; }

        public async Task<string> GetAuthorizationToken(ILog log, CancellationToken cancellationToken)
        {
            var authClientFactory = new AuthHttpClientFactory();

            var authContext = GetContextUri(ActiveDirectoryEndpointBaseUri, TenantId);
            log.Verbose($"Authentication Context: {authContext}");

            var app = ConfidentialClientApplicationBuilder.Create(ClientId)
                                                          .WithClientSecret(GetCredentials)
                                                          .WithAuthority(authContext)
                                                          .WithHttpClientFactory(authClientFactory)
                                                          .Build();

            var result = await app.AcquireTokenForClient(
                                                         new[] { $"{ResourceManagementEndpointBaseUri}/.default" })
                                  .WithTenantId(TenantId)
                                  .ExecuteAsync(cancellationToken)
                                  .ConfigureAwait(false);

            return result.AccessToken;
        }

        static string GetContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            if (!activeDirectoryEndPoint.EndsWith("/"))
            {
                return $"{activeDirectoryEndPoint}/{tenantId}";
            }

            return $"{activeDirectoryEndPoint}{tenantId}";
        }
    }
}
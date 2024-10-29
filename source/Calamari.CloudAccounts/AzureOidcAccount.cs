using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Identity.Client;
using Newtonsoft.Json;

namespace Calamari.CloudAccounts
{
    public class AzureOidcAccount : IAzureAccount
    {
        [JsonConstructor]
        public AzureOidcAccount(
            string subscriptionNumber,
            string clientId,
            string tenantId,
            string jwt,
            string azureEnvironment,
            string resourceManagementEndpointBaseUri,
            string activeDirectoryEndpointBaseUri)
        {
            SubscriptionNumber = subscriptionNumber;
            ClientId = clientId;
            TenantId = tenantId;
            Jwt = jwt;
            AzureEnvironment = azureEnvironment;
            ResourceManagementEndpointBaseUri = resourceManagementEndpointBaseUri;
            ActiveDirectoryEndpointBaseUri = activeDirectoryEndpointBaseUri;
        }

        public AzureOidcAccount(IVariables variables)
        {
            SubscriptionNumber = variables.Get(AccountVariables.SubscriptionId);
            ClientId = variables.Get(AccountVariables.ClientId);
            TenantId = variables.Get(AccountVariables.TenantId);
            Jwt = variables.Get(AccountVariables.Jwt);
            AzureEnvironment = variables.Get(AccountVariables.Environment);
            ResourceManagementEndpointBaseUri = variables.Get(AccountVariables.ResourceManagementEndPoint, DefaultAccountEndpoints.ResourceManagementEndpoint);
            ActiveDirectoryEndpointBaseUri = variables.Get(AccountVariables.ActiveDirectoryEndPoint, DefaultAccountEndpoints.OidcAuthContextUri);
        }

        public AccountType AccountType => AccountType.AzureOidc;
        public string GetCredentials => Jwt;

        public string SubscriptionNumber { get; }
        public string ClientId { get; }
        public string TenantId { get; }
        public string Jwt { get; }
        public string AzureEnvironment { get; }
        public string ResourceManagementEndpointBaseUri { get; }
        public string ActiveDirectoryEndpointBaseUri { get; }

        public async Task<string> GetAuthorizationToken(ILog log, CancellationToken cancellationToken)
        {
            var authClientFactory = new AuthHttpClientFactory();

            var authContext = GetOidcContextUri(string.IsNullOrEmpty(ActiveDirectoryEndpointBaseUri) ? "https://login.microsoftonline.com/" : ActiveDirectoryEndpointBaseUri, TenantId);
            log.Verbose($"Authentication Context: {authContext}");

            var app = ConfidentialClientApplicationBuilder.Create(ClientId)
                                                          .WithClientAssertion(GetCredentials)
                                                          .WithAuthority(authContext)
                                                          .WithHttpClientFactory(authClientFactory)
                                                          .Build();

            var result = await app.AcquireTokenForClient(
                                                         // Default values set on a per cloud basis on AzureOidcAccount, if managementEndPoint is set on the account /.default is required.
                                                         new[]
                                                         {
                                                             ResourceManagementEndpointBaseUri == DefaultAccountEndpoints.ResourceManagementEndpoint || string.IsNullOrEmpty(ResourceManagementEndpointBaseUri)
                                                                 ? AzureOidcAccount.GetDefaultScope(AzureEnvironment)
                                                                 : ResourceManagementEndpointBaseUri.EndsWith(".default")
                                                                     ? ResourceManagementEndpointBaseUri
                                                                     : $"{ResourceManagementEndpointBaseUri}/.default"
                                                         })
                                  .WithTenantId(TenantId)
                                  .ExecuteAsync(cancellationToken)
                                  .ConfigureAwait(false);

            return result.AccessToken;
        }

        static string GetOidcContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            if (!activeDirectoryEndPoint.EndsWith("/"))
            {
                return $"{activeDirectoryEndPoint}/{tenantId}/v2.0";
            }

            return $"{activeDirectoryEndPoint}{tenantId}/v2.0";
        }

        static string GetDefaultScope(string environmentName)
        {
            switch (environmentName)
            {
                case "AzureChinaCloud":
                    return "https://management.chinacloudapi.cn/.default";
                case "AzureGermanCloud":
                    return "https://management.microsoftazure.de/.default";
                case "AzureUSGovernment":
                    return "https://management.usgovcloudapi.net/.default";
                case "AzureGlobalCloud":
                case "AzureCloud":
                default:
                    // The double slash is intentional for public cloud.
                    return "https://management.azure.com//.default";
            }
        }
    }
}
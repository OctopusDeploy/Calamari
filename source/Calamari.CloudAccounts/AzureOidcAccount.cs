using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Rest;
using Newtonsoft.Json;
using NetWebRequest = System.Net.WebRequest;
using AzureEnvironmentEnum = Microsoft.Azure.Management.ResourceManager.Fluent.AzureEnvironment;

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
            ResourceManagementEndpointBaseUri = variables.Get(AccountVariables.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);
            ActiveDirectoryEndpointBaseUri = variables.Get(AccountVariables.ActiveDirectoryEndPoint, DefaultVariables.OidcAuthContextUri);
        }

        public AccountType AccountType => AccountType.AzureOidc;
        public string GetCredentials => Jwt;
        public string SubscriptionNumber { get;  }
        public string ClientId { get; }
        public string TenantId { get; }
        private string Jwt { get; }
        public string AzureEnvironment { get; }
        public string ResourceManagementEndpointBaseUri { get; }
        public string ActiveDirectoryEndpointBaseUri { get; }

        internal static string GetDefaultScope(string environmentName)
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

        public IAzure CreateAzureClient()
        {
            var environment = string.IsNullOrEmpty(AzureEnvironment) || AzureEnvironment == "AzureCloud"
                ? AzureEnvironmentEnum.AzureGlobalCloud
                : AzureEnvironmentEnum.FromName(AzureEnvironment) ??
                  throw new InvalidOperationException($"Unknown environment name {AzureEnvironment}");

            var accessToken = this.GetAuthorizationToken(CancellationToken.None).GetAwaiter().GetResult();
            var credentials = new AzureCredentials(
                                                   new TokenCredentials(accessToken),
                                                   new TokenCredentials(accessToken),
                                                   TenantId,
                                                   environment);

            // to ensure the Azure API uses the appropriate web proxy
            var client = new HttpClient(new HttpClientHandler {Proxy = NetWebRequest.DefaultWebProxy});

            return Microsoft.Azure.Management.Fluent.Azure.Configure()
                            .WithHttpClient(client)
                            .Authenticate(credentials)
                            .WithSubscription(SubscriptionNumber);
        }
    }
}
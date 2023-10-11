using System;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Identity.Client;
using Microsoft.Rest;
using AzureEnvironmentEnum = Microsoft.Azure.Management.ResourceManager.Fluent.AzureEnvironment;

namespace Calamari.CloudAccounts
{
    public static class AzureServicePrincipalAccountExtensions
    {
        public static async Task<ServiceClientCredentials> Credentials(this AzureServicePrincipalAccount account)
        {
            return new TokenCredentials(await GetAuthorizationToken(account));
        }

        public static Task<string> GetAuthorizationToken(this AzureServicePrincipalAccount account)
        {
            return GetAuthorizationToken(
                                         account.TenantId, 
                                         account.ClientId, 
                                         account.GetCredentials,
                                         account.ResourceManagementEndpointBaseUri, 
                                         account.ActiveDirectoryEndpointBaseUri);
        }

        public static async Task<string> GetAuthorizationToken(string tenantId, string applicationId, string password, string managementEndPoint, string activeDirectoryEndPoint)
        {
            var authClientFactory = new AuthHttpClientFactory();

            var authContext = GetContextUri(activeDirectoryEndPoint, tenantId);
            Log.Verbose($"Authentication Context: {authContext}");

            var app = ConfidentialClientApplicationBuilder.Create(applicationId)
                                                          .WithClientSecret(password)
                                                          .WithAuthority(authContext)
                                                          .WithHttpClientFactory(authClientFactory)
                                                          .Build();

            var result = await app.AcquireTokenForClient(
                                                         new [] { $"{managementEndPoint}/.default" })
                                  .WithTenantId(tenantId)
                                  .ExecuteAsync()
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

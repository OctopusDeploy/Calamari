using System;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Identity.Client;
using Microsoft.Rest;

namespace Calamari.CloudAccounts
{
    public static class AzureOidcAccountExtensions
    {
        public static async Task<ServiceClientCredentials> Credentials(this AzureOidcAccount account)
        {
            return new TokenCredentials(await GetAuthorizationToken(account));
        }
        
        public static Task<string> GetAuthorizationToken(this AzureOidcAccount account)
        {
            return GetAuthorizationToken(account.TenantId, account.ClientId, account.GetCredentials,
                                                   account.ResourceManagementEndpointBaseUri, account.ActiveDirectoryEndpointBaseUri);
        }

        public static async Task<string> GetAuthorizationToken(string tenantId, string applicationId, string token, string managementEndPoint, string activeDirectoryEndPoint)
        {
            var authContext = GetOidcContextUri("https://login.microsoftonline.com/", tenantId);
            Log.Verbose($"Authentication Context: {authContext}");

            var app = ConfidentialClientApplicationBuilder.Create(applicationId)
                                                          .WithClientAssertion(token)
                                                          .WithAuthority(authContext)
                                                          .Build();

            var result = await app.AcquireTokenForClient(
                                                         // Default values set on a per cloud basis on AzureOidcAccount, if managementEndPoint is set on the account /.default is required.
                                                         new[] { managementEndPoint })
                                  .WithTenantId(tenantId)
                                  .ExecuteAsync()
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
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Identity.Client;
using Microsoft.Rest;
using Newtonsoft.Json;

namespace Calamari.CloudAccounts
{
    public static class AzureOidcAccountExtensions
    {
        public static async Task<ServiceClientCredentials> Credentials(this AzureOidcAccount account, CancellationToken cancellationToken)
        {
            return new TokenCredentials(await GetAuthorizationToken(account, cancellationToken));
        }
        
        public static Task<string> GetAuthorizationToken(this AzureOidcAccount account, CancellationToken cancellationToken)
        {
            return GetAuthorizationToken(account.TenantId, account.ClientId, account.GetCredentials,
                                                   account.ResourceManagementEndpointBaseUri, account.ActiveDirectoryEndpointBaseUri, account.AzureEnvironment, cancellationToken);
        }

        public static async Task<string> GetAuthorizationToken(string tenantId, string applicationId, string token, string managementEndPoint, string activeDirectoryEndPoint, string aureEnvironment, CancellationToken cancellationToken)
        {
            Log.Info($"tenantId: {tenantId}");
            Log.Info($"applicationId: {applicationId}");
            Log.Info($"token: {token}");
            Log.Info($"managementEndPoint: {managementEndPoint}");
            Log.Info($"aureEnvironment: {aureEnvironment}");
            
            var authContext = GetOidcContextUri(activeDirectoryEndPoint, tenantId);
            Log.Verbose($"Authentication Context: {authContext}");
            
            var builder = ConfidentialClientApplicationBuilder.Create(applicationId)
                                                              .WithClientAssertion(token);

            if (activeDirectoryEndPoint.Contains("localhost"))
            {
                builder = builder.WithInstanceDiscoveryMetadata(new Uri($"{activeDirectoryEndPoint}/discovery"))
                                 .WithAuthority(authContext, false);
            }
            else
            {
                builder = builder.WithAuthority(authContext);
            }

            var app = builder.Build();

            var result = await app.AcquireTokenForClient(
                                                         // Default values set on a per cloud basis on AzureOidcAccount, if managementEndPoint is set on the account /.default is required.
                                                         new[] { managementEndPoint == DefaultVariables.ResourceManagementEndpoint ? AzureOidcAccount.GetDefaultScope(aureEnvironment) : managementEndPoint.EndsWith(".default") ? managementEndPoint : $"{managementEndPoint}/.default" })
                                  .WithTenantId(tenantId)
                                  .ExecuteAsync(cancellationToken)
                                  .ConfigureAwait(false);
            
            Log.Info($"auth result: {JsonConvert.SerializeObject(result)}");
            
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
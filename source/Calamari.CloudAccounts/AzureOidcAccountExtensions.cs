using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Identity.Client;
using Microsoft.Rest;

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
            return GetAuthorizationToken(
                                         account.TenantId,
                                         account.ClientId,
                                         account.GetCredentials,
                                         account.ResourceManagementEndpointBaseUri,
                                         account.ActiveDirectoryEndpointBaseUri,
                                         account.AzureEnvironment,
                                         account.InstanceDiscoveryUri,
                                         cancellationToken);
        }

        static async Task<string> GetAuthorizationToken(string tenantId, string applicationId, string token, string managementEndPoint, string activeDirectoryEndPoint, string aureEnvironment, string instanceDiscoveryUri, CancellationToken cancellationToken)
        {
            var authContext = GetOidcContextUri(string.IsNullOrEmpty(activeDirectoryEndPoint) ? "https://login.microsoftonline.com/" : activeDirectoryEndPoint, tenantId);
            Log.Verbose($"Authentication Context: {authContext}");
            
            var builder = ConfidentialClientApplicationBuilder.Create(applicationId).WithClientAssertion(token);

            if (!string.IsNullOrEmpty(instanceDiscoveryUri))
            {
                builder = builder.WithInstanceDiscoveryMetadata(new Uri(instanceDiscoveryUri))
                                 .WithAuthority(authContext, false);
            }
            else
            {
                builder = builder.WithAuthority(authContext);
            }

            var app = builder.Build();

            // Default values set on a per cloud basis on AzureOidcAccount, if managementEndPoint is set on the account /.default is required.
            var scope = managementEndPoint == DefaultVariables.ResourceManagementEndpoint || string.IsNullOrEmpty(managementEndPoint)
                ? AzureOidcAccount.GetDefaultScope(aureEnvironment)
                : managementEndPoint.EndsWith(".default")
                    ? managementEndPoint
                    : $"{managementEndPoint}/.default";

            var result = await app.AcquireTokenForClient(new[] { scope })
                                  .WithTenantId(tenantId)
                                  .ExecuteAsync(cancellationToken)
                                  .ConfigureAwait(false);

            return result.AccessToken;
        }

        static string GetOidcContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            activeDirectoryEndPoint = activeDirectoryEndPoint.TrimEnd('/');
            
            return $"{activeDirectoryEndPoint}/{tenantId}/v2.0";
        }
    }
}
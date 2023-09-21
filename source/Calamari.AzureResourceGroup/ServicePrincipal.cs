using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Identity.Client;

namespace Calamari.AzureResourceGroup
{
    static class ServicePrincipal
    {
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

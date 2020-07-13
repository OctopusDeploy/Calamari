using Calamari.Common.Plumbing.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Calamari.AzureWebApp
{
    class ServicePrincipal
    {
        public static string GetAuthorizationToken(string tenantId, string applicationId, string password, string managementEndPoint, string activeDirectoryEndPoint)
        {
            var authContext = GetContextUri(activeDirectoryEndPoint, tenantId);
            Log.Verbose($"Authentication Context: {authContext}");
            var context = new AuthenticationContext(authContext);
            var result = context.AcquireTokenAsync(managementEndPoint, new ClientCredential(applicationId, password)).GetAwaiter().GetResult();
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
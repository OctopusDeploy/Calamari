using Calamari.Shared;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Calamari.Azure.Integration.Security
{
    public class ServicePrincipal
    {
        private static ILog log = Log.Instance;
        
        public static string GetAuthorizationToken(string tenantId, string applicationId, string password, string managementEndPoint, string activeDirectoryEndPoint)
        {
            var authContext = GetContextUri(activeDirectoryEndPoint, tenantId);
            log.Verbose($"Authentication Context: {authContext}");
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
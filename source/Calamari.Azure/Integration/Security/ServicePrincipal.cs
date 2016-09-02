using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Calamari.Azure.Integration.Security
{
    public class ServicePrincipal
    {
     
        public static string GetAuthorizationToken(string tenantId, string applicationId, string password, string serviceManagementEndPoint, string activeDirectoryEndPoint)
        {
            var context = new AuthenticationContext($"{activeDirectoryEndPoint}/{tenantId}");
            var result = context.AcquireToken(serviceManagementEndPoint, new ClientCredential(applicationId, password));
            return result.AccessToken;
        }
    }
}
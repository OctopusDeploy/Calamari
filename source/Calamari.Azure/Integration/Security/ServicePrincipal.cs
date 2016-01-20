using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Calamari.Azure.Integration.Security
{
    public class ServicePrincipal
    {
        public static string GetAuthorizationToken(string tenantId, string applicationId, string password)
        {
            var context = new AuthenticationContext($"https://login.windows.net/{tenantId}");
            var result = context.AcquireToken("https://management.core.windows.net/", new ClientCredential(applicationId, password));
            return result.AccessToken;
        }
    }
}
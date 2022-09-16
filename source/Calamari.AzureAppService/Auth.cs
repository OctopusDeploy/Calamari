using System;
using System.Text;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Calamari.AzureAppService
{
    internal class Auth
    {
        public static async Task<string> GetBasicAuthCreds(ServicePrincipalAccount principalAccount,
            TargetSite targetSite)
        {
            var publishingProfile = await PublishingProfile.GetPublishingProfile(targetSite, principalAccount);
            string credential =
                Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{publishingProfile.Username}:{publishingProfile.Password}"));
            return credential;
        }

        public static async Task<string> GetAuthTokenAsync(ServicePrincipalAccount principalAccount)
        {
            string authContext =
                GetContextUri(principalAccount.ActiveDirectoryEndpointBaseUri, principalAccount.TenantId);
            var context = new AuthenticationContext(authContext);
            var result = await context.AcquireTokenAsync(principalAccount.ResourceManagementEndpointBaseUri,
                new ClientCredential(principalAccount.ClientId, principalAccount.Password));
            return result.AccessToken;
        }

        public static async Task<string> GetAuthTokenAsync(string ADEndpointBaseUri, string resourceMgmtEndpointBaseUri,
            string tenantId, string clientId, string clientSecret)
        {
            string authContext = GetContextUri(ADEndpointBaseUri, tenantId);
            var context = new AuthenticationContext(authContext);
            var result = await context.AcquireTokenAsync(resourceMgmtEndpointBaseUri,
                new ClientCredential(clientId, clientSecret));
            return result.AccessToken;
        }

        private static string GetContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            if (!activeDirectoryEndPoint.EndsWith("/")) return $"{activeDirectoryEndPoint}/{tenantId}";
            return $"{activeDirectoryEndPoint}{tenantId}";
        }
    }
}

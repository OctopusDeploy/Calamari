using System;
using System.Text;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using ClientCredential = Microsoft.IdentityModel.Clients.ActiveDirectory.ClientCredential;

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

        public static async Task<string> GetAuthTokenAsync(string tenantId, string applicationId, string password, string managementEndPoint, string activeDirectoryEndPoint)
        { 
            var authContext = GetContextUri(activeDirectoryEndPoint, tenantId);

            var app = ConfidentialClientApplicationBuilder.Create(applicationId)
                                                          .WithClientSecret(password)
                                                          .WithAuthority(authContext)
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

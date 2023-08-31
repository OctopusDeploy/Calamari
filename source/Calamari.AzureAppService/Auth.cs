using System;
using System.Text;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Microsoft.Identity.Client;

namespace Calamari.AzureAppService
{
    internal class Auth
    {
        public static async Task<string> GetBasicAuthCreds(IAzureAccount azureAccount,
            AzureTargetSite targetSite)
        {
            var publishingProfile = await PublishingProfile.GetPublishingProfile(targetSite, azureAccount);
            string credential =
                Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{publishingProfile.Username}:{publishingProfile.Password}"));
            return credential;
        }

        public static async Task<string> GetAuthTokenAsync(IAzureAccount azureAccount)
        {
            if (azureAccount.AccountType == AccountType.AzureOidc)
            {
                return azureAccount.GetCredential;
            }
            return await GetServicePrincipalAuthTokenAsync(azureAccount.TenantId, azureAccount.ClientId, azureAccount.GetCredential, azureAccount.ResourceManagementEndpointBaseUri, azureAccount.ActiveDirectoryEndpointBaseUri);
        }

        public static async Task<string> GetServicePrincipalAuthTokenAsync(string tenantId, string applicationId, string password, string managementEndPoint, string activeDirectoryEndPoint)
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

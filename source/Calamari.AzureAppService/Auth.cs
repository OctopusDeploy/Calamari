using System;
using System.Text;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;

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
                return await ((AzureOidcAccount)azureAccount).GetAuthorizationToken();
            }

            return await ((AzureServicePrincipalAccount)azureAccount).GetAuthorizationToken();
        }
    }
}

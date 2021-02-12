using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calamari.Azure;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;

namespace Calamari.AzureAppService
{
    class Auth
    {
        public static async Task<string> GetBasicAuthCreds(ServicePrincipalAccount principalAccount, TargetSite targetSite, string resourceGroupName)
        {
            var (username, password) = await GetPublishProfileCredsAsync(targetSite, principalAccount, resourceGroupName);
            var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            return credential;
        }

        public static async Task<string> GetAuthTokenAsync(ServicePrincipalAccount principalAccount)
        {
            var authContext = GetContextUri(principalAccount.ActiveDirectoryEndpointBaseUri, principalAccount.TenantId);
            var context = new AuthenticationContext(authContext);
            var result = await context.AcquireTokenAsync(principalAccount.ResourceManagementEndpointBaseUri, new ClientCredential(principalAccount.ClientId, principalAccount.Password));
            return result.AccessToken;
        }

        private static string GetContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            if (!activeDirectoryEndPoint.EndsWith("/"))
            {
                return $"{activeDirectoryEndPoint}/{tenantId}";
            }
            return $"{activeDirectoryEndPoint}{tenantId}";
        }

        private static async Task<(string Username, string Password)> GetPublishProfileCredsAsync(TargetSite targetSite, ServicePrincipalAccount account, string resourceGroupName)
        {
            var mgmtEndpoint = account.ResourceManagementEndpointBaseUri;
            var token = await GetAuthTokenAsync(account);

            var webAppClient = new WebSiteManagementClient(new Uri(mgmtEndpoint), new TokenCredentials(token))
            { SubscriptionId = account.SubscriptionNumber };

            var options = new CsmPublishingProfileOptions { Format = "WebDeploy" };
            var resourceGroup = resourceGroupName; //variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);


            webAppClient.WebApps.Get(resourceGroup, targetSite.Site);

            using var publishProfileStream = targetSite.HasSlot
                ? await webAppClient.WebApps.ListPublishingProfileXmlWithSecretsSlotAsync(resourceGroup,
                    targetSite.Site, options, targetSite.Slot)
                : await webAppClient.WebApps.ListPublishingProfileXmlWithSecretsAsync(resourceGroup, targetSite.Site,
                    options);

            using var streamReader = new StreamReader(publishProfileStream);
            var document = XDocument.Parse(await streamReader.ReadToEndAsync());

            var profile = (from el in document.Descendants("publishProfile")
                           where string.Compare(el.Attribute("publishMethod")?.Value, "MSDeploy", StringComparison.OrdinalIgnoreCase) == 0
                           select new
                           {
                               PublishUrl = $"https://{el.Attribute("publishUrl")?.Value}",
                               Username = el.Attribute("userName")?.Value,
                               Password = el.Attribute("userPWD")?.Value,
                               Site = el.Attribute("msdeploySite")?.Value
                           }).FirstOrDefault();

            if (profile == null)
            {
                throw new Exception("Failed to retrieve publishing profile.");
            }

            return (profile.Username, profile.Password);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calamari.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.Win32.SafeHandles;

namespace Calamari.AzureWebAppZip
{
    class AzureWebAppZipBehaviour : IDeployBehaviour
    {
        private ILog Log { get; }

        public AzureWebAppZipBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            Log.Verbose("Starting ZipDeploy");
            var variables = context.Variables;
            var principalAccount = new ServicePrincipalAccount(variables);

            var targetSite = AzureWebAppHelper.GetAzureTargetSite(
                variables.Get(SpecialVariables.Action.Azure.WebAppName),
                variables.Get(SpecialVariables.Action.Azure.WebAppSlot));

            var token = await GetAuthTokenAsync(principalAccount);
            Log.Verbose($"Token: {token}");
            var creds = await GetPublishProfileCredsAsync(targetSite, new TokenCredentials(token), principalAccount,
                variables);

            Log.Verbose($"UN: {creds.Username}\nPWD: {creds.Password}");

            var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{creds.Username}:{creds.Password}"));

            Log.Verbose($"Base64 Cred: {credential}");

            var client2 = new HttpClient();
            client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credential);
            client2.DefaultRequestHeaders.Add("contentType","application/zip");

            var uploadZipPath = variables.Get(TentacleVariables.CurrentDeployment.PackageFilePath);
            Log.Verbose($"Path to upload: {uploadZipPath}");
            Log.Verbose($"Target Site: {targetSite.Site}");

            if (!new FileInfo(uploadZipPath).Exists)
                throw new FileNotFoundException(uploadZipPath);

            try
            {
                Log.Verbose($@"Publishing {uploadZipPath} to https://{targetSite.Site}.scm.azurewebsites.net/api/zipdeploy");
                var response = await client2.PostAsync($@"https://{targetSite.Site}.scm.azurewebsites.net/api/zipdeploy",
                    new StreamContent(new FileStream(uploadZipPath, FileMode.Open)));
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(response.ReasonPhrase);
                }

                Log.Verbose("Finished deploying");
            }
            finally
            {
                client2.Dispose();
            }
        }

        private async Task<string> GetAuthTokenAsync(ServicePrincipalAccount principalAccount)// ,string tenantId, string applicationId, string password)
        {
            var authContext = GetContextUri(principalAccount.ActiveDirectoryEndpointBaseUri, principalAccount.TenantId);
            //Log.Verbose($"Authentication Context: {authContext}");
            var context = new AuthenticationContext(authContext);
            var result = await context.AcquireTokenAsync(principalAccount.ResourceManagementEndpointBaseUri, new ClientCredential(principalAccount.ClientId, principalAccount.Password));
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


        private async Task<(string Username, string Password)> GetPublishProfileCredsAsync(TargetSite targetSite, TokenCredentials tokenCredentials, ServicePrincipalAccount account, IVariables variables)
        {

            var mgmtEndpoint = account.ResourceManagementEndpointBaseUri;

            var webAppClient = new WebSiteManagementClient(new Uri(mgmtEndpoint), tokenCredentials)
                {SubscriptionId = account.SubscriptionNumber};

            var options = new CsmPublishingProfileOptions {Format = "WebDeploy"};
            var resourceGroup = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);

            
            webAppClient.WebApps.Get(resourceGroup, targetSite.Site);

            using var publishProfileStream = targetSite.HasSlot
                ? await webAppClient.WebApps.ListPublishingProfileXmlWithSecretsSlotAsync(resourceGroup,
                    targetSite.Site, options, targetSite.Slot)
                : await webAppClient.WebApps.ListPublishingProfileXmlWithSecretsAsync(resourceGroup, targetSite.Site,
                    options);

            //await using var publishProfileStream = targetSite.HasSlot
            //    ? await webAppClient.WebApps.ListPublishingProfileXmlWithSecretsSlotAsync(resourceGroup,
            //        targetSite.Site, options, targetSite.Slot)
            //    : await webAppClient.WebApps.ListPublishingProfileXmlWithSecretsAsync(resourceGroup, targetSite.Site,
            //        options);

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

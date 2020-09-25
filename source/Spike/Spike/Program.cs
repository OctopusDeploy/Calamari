using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;

namespace Spike
{
    class Program
    {
        static void Main(string[] args)
        {
            var userName = "$CMOcto"; //"0f91c747-93cf-464e-9a6c-d46c93eef239";
            var clientId = "0f91c747-93cf-464e-9a6c-d46c93eef239";

            var pwd = "8BFXLK7s7xl0ArGqpcpbF4L7PjsdfCECyiGHtzuC7awdvNjG5MFYzQcM6hPF"; //"EU.M~6P3pCHe4K__x3~jif.keOtae5A7Xz";
            var clientSecret = "EU.M~6P3pCHe4K__x3~jif.keOtae5A7Xz";

            var tenantId = "27312afb-009f-4fed-a8bb-9737425cc42a";

            var appUrl = @"https://cmocto.scm.azurewebsites.net/api/zipdeploy";
            var zipPath = @"..\..\..\..\..\Calamari.Tests\Packages\HelloWorld.zip";

            //var authResponse = GetAuthToken(clientId, tenantId, clientSecret).Result;
            var token = GetAuthToken(tenantId, clientId, clientSecret);

            var creds = GetPublishProfileCreds(new TokenCredentials(token), "chrisplayground",
                "cmocto");
            var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{creds.UserName}:{creds.Pwd}"));


            var webClient = new WebClient {Headers = {[HttpRequestHeader.Authorization] = $"Basic {credential}"}};
            webClient.Headers[HttpRequestHeader.ContentType] = "application/zip";
            byte[] response = null;

            try
            {
                response = webClient.UploadData(appUrl, File.ReadAllBytes(zipPath));
            }
            catch (Exception ex)
            {
                if (response != null)
                {
                    Console.WriteLine(response);
                }
                throw ex;
            }
        }

        #region [ ServicePrincipal.cs ]
        
        private static string GetAuthToken(string tenantId, string applicationId, string password)
        {
            var activeDirectoryEndPoint = @"https://login.windows.net/";
            var managementEndPoint = @"https://management.azure.com/";
            var authContext = GetContextUri(activeDirectoryEndPoint, tenantId);
            //Log.Verbose($"Authentication Context: {authContext}");
            var context = new AuthenticationContext(authContext);
            var result = context.AcquireTokenAsync(managementEndPoint, new Microsoft.IdentityModel.Clients.ActiveDirectory.ClientCredential(applicationId, password)).Result;
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

        #endregion

        static PublishProfileCreds GetPublishProfileCreds(TokenCredentials tokenCredentials, string resourceGroupName, string siteName)
        {
            var resourceManagementEndpoint = "https://management.azure.com/";
            //var scopes = new[] { "user.read" };
            //var app = ConfidentialClientApplicationBuilder

            var webAppClient = new WebSiteManagementClient(new Uri(resourceManagementEndpoint), tokenCredentials)
                {SubscriptionId = "84d2a09b-50bf-4ad3-98ec-4bc2bec36820"};

            var options = new CsmPublishingProfileOptions {Format = "WebDeploy"};
            using var stream = webAppClient.WebApps
                .ListPublishingProfileXmlWithSecretsAsync(resourceGroupName, siteName, options).Result;

            using var streamReader = new StreamReader(stream);
            var text = streamReader.ReadToEnd();

            var document = XDocument.Parse(text);

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

            return new PublishProfileCreds(profile.Username, profile.Password);
        }
    }
}

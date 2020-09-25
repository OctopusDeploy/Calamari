using System;

using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.Azure.Publishing;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Identity.Client;
using NuGet.Protocol;

//using Microsoft.IdentityModel.Clients.ActiveDirectory;
//using AuthenticationResult = Microsoft.Identity.Client.AuthenticationResult;

namespace Calamari.AzureWebAppZip
{
    class AzureWebAppZipBehaviour : IDeployBehaviour
    {
        readonly ILog log;

        public AzureWebAppZipBehaviour(ILog log)
        {
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var appName = "cmocto"; //variables.Get("appName");

            var targetUrl = $"https://{appName}.scm.azurewebsites.net/api/zipdeploy";

            var webClient = new WebClient();

            var username = "0f91c747-93cf-464e-9a6c-d46c93eef239"; //deployment.Variables.Get(AzureAccountVariables.ClientId);
            var password = deployment.Variables.Get(AzureAccountVariables.Password);
            var credential = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            var zipPath = deployment.Variables.Get("Octopus.Test.PackagePath");
            //var tenantId = "27312afb-009f-4fed-a8bb-9737425cc42a";

            var authResult = await GetAuthToken("0f91c747-93cf-464e-9a6c-d46c93eef239",
                "27312afb-009f-4fed-a8bb-9737425cc42a", "EU.M~6P3pCHe4K__x3~jif.keOtae5A7Xz");

            webClient.Headers[HttpRequestHeader.Authorization] = $"Basic {credential}";
            webClient.Headers[HttpRequestHeader.ContentType] = "application/zip";
            

            byte[] response;

            try
            {
                //var response = await webClient.UploadFileTaskAsync(new Uri(targetUrl), "POST", zipPath); //PackageVariables.Output.FilePath));
                response = await webClient.UploadFileTaskAsync(new Uri(targetUrl), "POST", zipPath);

                while (webClient.IsBusy)
                    await Task.Delay(1000);
            }
            catch (WebException ex)
            {
                throw ex;
            }

            //var variables = deployment.Variables;
            //var subscriptionId = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            //var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName, string.Empty);
            //var siteAndSlotName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            //var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);

            //var targetSite = AzureWebAppHelper.GetAzureTargetSite(siteAndSlotName, slotName);

            //var resourceGroupText = string.IsNullOrEmpty(resourceGroupName)
            //    ? string.Empty
            //    : $" in Resource Group '{resourceGroupName}'";
            //var slotText = targetSite.HasSlot
            //    ? $", deployment slot '{targetSite.Slot}'"
            //    : string.Empty;
            //log.Info(
            //    $"Deploying to Azure WebApp '{targetSite.Site}'{slotText}{resourceGroupText}, using subscription-id '{subscriptionId}'");

            //RemoteCertificateValidationCallback originalServerCertificateValidationCallback = null;
            //try
            //{
            //    originalServerCertificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;
            //    ServicePointManager.ServerCertificateValidationCallback = WrapperForServerCertificateValidationCallback;
            //    await Task.Run(() => { Console.WriteLine("Hello"); });
            //}
            //finally
            //{
            //    ServicePointManager.ServerCertificateValidationCallback = originalServerCertificateValidationCallback;
            //}

        }

        bool WrapperForServerCertificateValidationCallback(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            switch (sslPolicyErrors)
            {
                case SslPolicyErrors.None:
                    return true;
                case SslPolicyErrors.RemoteCertificateNameMismatch:
                    log.Error(
                        $"A certificate mismatch occurred. We have had reports previously of Azure using incorrect certificates for some Web App SCM sites, which seem to related to a known issue, a possible fix is documented in {log.FormatLink("https://g.octopushq.com/CertificateMismatch")}.");
                    break;
            }

            return false;
        }

        //private Task<WebDeployPublishSettings> GetWebDeployPublishProfile(WebSiteManagementClient webSiteClient, string resourceGroupName, string appName)
        //{
        //    var authSettings = webSiteClient.WebApps.GetAuthSettings(resourceGroupName, appName).;
        //}

        private async Task<AuthenticationResult> GetAuthToken(string clientId, string tenantId, string clientSecret)
        {
            var scopes = new[] {"https://graph.microsoft.com/.default"};
            var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                .WithClientSecret(clientSecret)
                .WithTenantId(tenantId)
                .Build();

            return await app.AcquireTokenForClient(scopes).ExecuteAsync();
        }
    }
}
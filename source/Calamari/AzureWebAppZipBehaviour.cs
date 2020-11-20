#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureAppService.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using Newtonsoft.Json;

namespace Calamari.AzureAppService
{
    class AzureAppServiceBehaviour : IDeployBehaviour
    {
        private ILog Log { get; }

        public AzureAppServiceBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        { 
            // Read/Validate variables
            Log.Verbose("Starting ZipDeploy");
            var variables = context.Variables;
            var principalAccount = new ServicePrincipalAccount(variables);

            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);

            if (webAppName == null)
                throw new Exception("Web App Name must be specified");

            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);

            if (resourceGroupName == null)
                throw new Exception("resource group name must be specified");

            var uploadZipPath = variables.Get(TentacleVariables.CurrentDeployment.PackageFilePath);

            if (uploadZipPath == null)
                throw new Exception("Package File Path must be specified");

            var targetSite = AzureWebAppHelper.GetAzureTargetSite(webAppName, slotName);

            // Get Authentication creds/tokens
            var credential = await Auth.GetBasicAuthCreds(principalAccount, targetSite, resourceGroupName);
            string token = await Auth.GetAuthTokenAsync(principalAccount);

            var webAppClient = new WebSiteManagementClient(new Uri(DefaultVariables.ResourceManagementEndpoint), new TokenCredentials(token))
                { SubscriptionId = principalAccount.SubscriptionNumber};

            List<Task> taskList = new List<Task>();

            var appSettings = JsonConvert.DeserializeObject<AppSettingsRoot>(variables.Get(SpecialVariables.Action.Azure.AppSettings));
            if (appSettings.HasSettings)
                taskList.Add(PublishAppSettings(webAppClient, resourceGroupName, webAppName, appSettings, token,
                    slotName));

            var httpClient = webAppClient.HttpClient;
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credential);

            taskList.Add(UploadZipAsync(httpClient, uploadZipPath, targetSite.Site));

            Task.WaitAll(taskList.ToArray());

            await webAppClient.WebApps.RestartAsync(resourceGroupName, webAppName, true);
        }

        private async Task UploadZipAsync(HttpClient client, string uploadZipPath, string targetSite)
        {
            Log.Verbose($"Path to upload: {uploadZipPath}");
            Log.Verbose($"Target Site: {targetSite}");

            if (!new FileInfo(uploadZipPath).Exists)
                throw new FileNotFoundException(uploadZipPath);

            Log.Verbose($@"Publishing {uploadZipPath} to https://{targetSite}.scm.azurewebsites.net/api/zipdeploy");

            var response = await client.PostAsync($@"https://{targetSite}.scm.azurewebsites.net/api/zipdeploy",
                new StreamContent(new FileStream(uploadZipPath, FileMode.Open)));

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(response.ReasonPhrase);
            }

            Log.Verbose("Finished deploying");
        }

        private async Task PublishAppSettings(WebSiteManagementClient webAppClient, string resourceGroupName,
            string webAppName, AppSettingsRoot appSettings, string authToken, string? slotName)
        {
            var settingsDict = new StringDictionary
            {
                Properties = new Dictionary<string, string>()
            };

            foreach (var setting in appSettings.AppSettings)
            {
                settingsDict.Properties[setting.Name] = setting.Value;
            }

            await AppSettingsManagement.PatchAppSettings(webAppClient, resourceGroupName, webAppName, settingsDict,
                slotName);
            var slotSettings = appSettings.AppSettings
                .Where(setting => setting.IsSlotSetting)
                .Select(setting => setting.Name);

            var existingSlotSettings =
                await AppSettingsManagement.GetSlotSettingsList(webAppClient, resourceGroupName, webAppName, authToken);

            if (!slotSettings.Any() || existingSlotSettings.Any())
                return;

            await AppSettingsManagement.PutSlotSettingsList(webAppClient, resourceGroupName, webAppName,
                slotSettings.Concat(existingSlotSettings), authToken);
        }
    }
}

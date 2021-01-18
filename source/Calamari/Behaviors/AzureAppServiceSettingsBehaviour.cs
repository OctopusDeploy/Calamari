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

namespace Calamari.AzureAppService.Behaviors
{
    class AzureAppServiceSettingsBehaviour : IDeployBehaviour
    {
        private ILog Log { get; }

        public AzureAppServiceSettingsBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return !string.IsNullOrEmpty(context.Variables.Get(SpecialVariables.Action.Azure.AppSettings));
        }

        public async Task Execute(RunningDeployment context)
        { 
            // Read/Validate variables
            Log.Verbose("Starting App Settings Deploy");
            var variables = context.Variables;
            var principalAccount = new ServicePrincipalAccount(variables);

            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);

            if (webAppName == null)
                throw new Exception("Web App Name must be specified");

            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);

            if (resourceGroupName == null)
                throw new Exception("resource group name must be specified");

            string token = await Auth.GetAuthTokenAsync(principalAccount);

            var webAppClient = new WebSiteManagementClient(new Uri(principalAccount.ResourceManagementEndpointBaseUri),
                new TokenCredentials(token))
            {
                SubscriptionId = principalAccount.SubscriptionNumber,
                HttpClient = {BaseAddress = new Uri(principalAccount.ResourceManagementEndpointBaseUri)}
            };

            var appSettings = JsonConvert.DeserializeObject<AppSettingsRoot>(variables.Get(SpecialVariables.Action.Azure.AppSettings));
            
            Log.Verbose($"Deploy publishing app settings to webapp {webAppName} in resource group {resourceGroupName}");

            await PublishAppSettings(webAppClient, resourceGroupName, webAppName, appSettings, token, slotName);

            if (string.IsNullOrEmpty(slotName))
            {
                Log.Info($"Soft restarting {webAppName} in resource group {resourceGroupName}");
                await webAppClient.WebApps.RestartAsync(resourceGroupName, webAppName, true);

            }
            else
            {
                Log.Info($"Soft restarting slot {slotName} in app {webAppName} in resource group {resourceGroupName}");
                await webAppClient.WebApps.RestartSlotAsync(resourceGroupName, webAppName, slotName, true);
            }
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

            await AppSettingsManagement.PatchAppSettingsAsync(webAppClient, resourceGroupName, webAppName, settingsDict,
                slotName);
            var slotSettings = appSettings.AppSettings
                .Where(setting => setting.IsSlotSetting)
                .Select(setting => setting.Name).ToArray();

            var existingSlotSettings =
                (await AppSettingsManagement.GetSlotSettingsListAsync(webAppClient, resourceGroupName, webAppName,
                    authToken)).ToArray();

            if (!slotSettings.Any() || existingSlotSettings.Any())
                return;

            await AppSettingsManagement.PutSlotSettingsListAsync(webAppClient, resourceGroupName, webAppName,
                slotSettings.Concat(existingSlotSettings), authToken);
        }
    }
}

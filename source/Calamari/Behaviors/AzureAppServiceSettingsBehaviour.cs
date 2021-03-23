#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureAppService.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
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

            //if there are no app settings to deploy
            if (!variables.GetNames().Contains(SpecialVariables.Action.Azure.AppSettings) &&
                !string.IsNullOrEmpty(variables[SpecialVariables.Action.Azure.AppSettings]))
                return;

            var principalAccount = new ServicePrincipalAccount(variables);

            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);

            if (webAppName == null)
                throw new Exception("Web App Name must be specified");

            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);

            if (resourceGroupName == null)
                throw new Exception("resource group name must be specified");

            var targetSite = AzureWebAppHelper.GetAzureTargetSite(webAppName, slotName, resourceGroupName);


            string token = await Auth.GetAuthTokenAsync(principalAccount);

            var webAppClient = new WebSiteManagementClient(new Uri(principalAccount.ResourceManagementEndpointBaseUri),
                new TokenCredentials(token))
            {
                SubscriptionId = principalAccount.SubscriptionNumber,
                HttpClient = {BaseAddress = new Uri(principalAccount.ResourceManagementEndpointBaseUri)}
            };

            var appSettings =
                JsonConvert.DeserializeObject<AppSetting[]>(variables.Get(SpecialVariables.Action.Azure.AppSettings, ""))
                //?? new AppSetting[] { };
                ;

            Log.Verbose($"Deploy publishing app settings to webapp {webAppName} in resource group {resourceGroupName}");

            // publish defined settings (automatically merges with existing settings
            await PublishAppSettings(webAppClient, targetSite, appSettings, token);
        }

        /// <summary>
        /// combines and publishes app and slot settings
        /// </summary>
        /// <param name="webAppClient"></param>
        /// <param name="targetSite"></param>
        /// <param name="appSettings"></param>
        /// <param name="authToken"></param>
        /// <returns></returns>
        private async Task PublishAppSettings(WebSiteManagementClient webAppClient, TargetSite targetSite,
            AppSetting[] appSettings, string authToken)
        {
            var settingsDict = new StringDictionary
            {
                Properties = new Dictionary<string, string>()
            };

            var existingSlotSettings = new List<string>();
            foreach (var (name, value, SlotSetting) in (await AppSettingsManagement.GetAppSettingsAsync(webAppClient,
                authToken, targetSite)).ToList())
            {
                settingsDict.Properties[name] = value;
                if (SlotSetting)
                    existingSlotSettings.Add(name);
            }

            foreach (var setting in appSettings)
            {
                settingsDict.Properties[setting.Name] = setting.Value;
            }

            await AppSettingsManagement.PutAppSettingsAsync(webAppClient, settingsDict, targetSite);
            var slotSettings = appSettings
                .Where(setting => setting.SlotSetting)
                .Select(setting => setting.Name).ToArray();

            if (!slotSettings.Any())
                return;

            await AppSettingsManagement.PutSlotSettingsListAsync(webAppClient, targetSite,
                slotSettings.Union(existingSlotSettings), authToken);
        }
    }
}

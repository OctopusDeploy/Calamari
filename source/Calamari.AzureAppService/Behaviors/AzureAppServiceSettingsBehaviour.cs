#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService.Models;
using Calamari.AzureAppService.Azure;
using Calamari.AzureAppService.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
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
            return
                !string.IsNullOrWhiteSpace(context.Variables.Get(SpecialVariables.Action.Azure.AppSettings)) || !string.IsNullOrWhiteSpace(context.Variables.Get(SpecialVariables.Action.Azure.ConnectionStrings));
        }

        public async Task Execute(RunningDeployment context)
        {
            // Read/Validate variables
            Log.Verbose("Starting App Settings Deploy");
            var variables = context.Variables;

            var principalAccount = ServicePrincipalAccount.CreateFromKnownVariables(variables);

            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);

            if (webAppName == null)
                throw new Exception("Web App Name must be specified");

            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);

            if (resourceGroupName == null)
                throw new Exception("resource group name must be specified");

            var targetSite = new AzureTargetSite(principalAccount.SubscriptionNumber, resourceGroupName, webAppName, slotName);

            var armClient = principalAccount.CreateArmClient();

            // If app settings are specified
            if (variables.GetNames().Contains(SpecialVariables.Action.Azure.AppSettings) && !string.IsNullOrWhiteSpace(variables[SpecialVariables.Action.Azure.AppSettings]))
            {
                var appSettingsJson = variables.Get(SpecialVariables.Action.Azure.AppSettings, "");
                Log.Verbose($"Updating application settings:\n{appSettingsJson}");
                var appSettings = JsonConvert.DeserializeObject<AppSetting[]>(appSettingsJson);
                await PublishAppSettings(armClient, principalAccount, targetSite, appSettings);
                Log.Info("Updated application settings");
            }

            // If connection strings are specified
            if (variables.GetNames().Contains(SpecialVariables.Action.Azure.ConnectionStrings) && !string.IsNullOrWhiteSpace(variables[SpecialVariables.Action.Azure.ConnectionStrings]))
            {
                var connectionStringsJson = variables.Get(SpecialVariables.Action.Azure.ConnectionStrings, "");
                Log.Verbose($"Updating connection strings:\n{connectionStringsJson}");
                var connectionStrings = JsonConvert.DeserializeObject<ConnectionStringSetting[]>(connectionStringsJson);
                await PublishConnectionStrings(armClient, principalAccount, targetSite, connectionStrings);
                Log.Info("Updated connection strings");
            }
        }

        /// <summary>
        /// combines and publishes app and slot settings
        /// </summary>
        /// <param name="armClient"></param>
        /// <param name="targetSite"></param>
        /// <param name="appSettings"></param>
        /// <param name="authToken"></param>
        /// <returns></returns>
        private async Task PublishAppSettings(ArmClient armClient, ServicePrincipalAccount principalAccount, AzureTargetSite targetSite, AppSetting[] appSettings)
        {
            var settingsDict = new AppServiceConfigurationDictionary();

            var existingSlotSettings = new List<string>();
            // for each app setting found on the web app (existing app setting) update it value and add if is a slot setting, add
            foreach (var (name, value, slotSetting) in await armClient.GetAppSettingsListAsync(targetSite))
            {
                settingsDict.Properties[name] = value;

                if (slotSetting)
                    existingSlotSettings.Add(name);
            }

            // for each app setting defined by the user
            foreach (var setting in appSettings)
            {
                // add/update the settings value
                settingsDict.Properties[setting.Name] = setting.Value;

                // if the user indicates a settings should no longer be a slot setting
                if (existingSlotSettings.Contains(setting.Name) && !setting.SlotSetting)
                {
                    Log.Verbose($"Removing {setting.Name} from the list of slot settings");
                    existingSlotSettings.Remove(setting.Name);
                }
            }

            await armClient.UpdateAppSettingsAsync(targetSite, settingsDict);
            var slotSettings = appSettings
                               .Where(setting => setting.SlotSetting)
                               .Select(setting => setting.Name)
                               .ToArray();

            if (!slotSettings.Any())
                return;

            await armClient.UpdateSlotSettingsAsync(targetSite, slotSettings.Union(existingSlotSettings));
        }

        private async Task PublishConnectionStrings(ArmClient armClient,
                                                    ServicePrincipalAccount servicePrincipalAccount,
                                                    AzureTargetSite targetSite,
                                                    ConnectionStringSetting[] newConStrings)
        {
            var conStrings = await armClient.GetConnectionStringsAsync(targetSite);

            foreach (var connectionStringSetting in newConStrings)
            {
                conStrings.Properties[connectionStringSetting.Name] =
                    new ConnStringValueTypePair(connectionStringSetting.Value, connectionStringSetting.Type);
            }

            await armClient.UpdateConnectionStringsAsync(targetSite, conStrings);
        }
    }
}
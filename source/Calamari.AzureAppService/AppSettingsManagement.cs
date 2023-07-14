#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Calamari.AzureAppService.Azure;
using Calamari.AzureAppService.Json;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AzureAppService
{
    internal static class AppSettingsManagement
    {
        public static async Task<IEnumerable<AppSetting>> GetAppSettingsAsync(ArmClient armClient, ServicePrincipalAccount servicePrincipalAccount, TargetSite targetSite)
        {
            AppServiceConfigurationDictionary appSettings = targetSite.HasSlot switch
                                                            {
                                                                true => await armClient.GetWebSiteSlotResource(WebSiteSlotResource.CreateResourceIdentifier(servicePrincipalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site, targetSite.Slot))
                                                                                       .GetApplicationSettingsSlotAsync(),
                                                                false => await armClient.GetWebSiteResource(WebSiteResource.CreateResourceIdentifier(servicePrincipalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site))
                                                                                        .GetApplicationSettingsAsync()
                                                            };

            var slotSettings = (await GetSlotSettingsListAsync(armClient, servicePrincipalAccount, targetSite)).ToArray();

            return appSettings.Properties.Select(
                                                 setting =>
                                                     new AppSetting
                                                     {
                                                         Name = setting.Key,
                                                         Value = setting.Value,
                                                         SlotSetting = slotSettings.Any(x => x == setting.Key)
                                                     })
                              .ToList();
        }

        /// <summary>
        /// Patches (add or update) the app settings for a web app or slot using the website management client library extensions.
        /// If any setting needs to be marked sticky (slot setting), update it via <see cref="PutSlotSettingsListAsync"/>.
        /// </summary>
        /// <param name="armClient">A <see cref="WebSiteManagementClient"/> that is directly used to update app settings.<seealso cref="WebSiteManagementClientExtensions"/></param>
        /// <param name="targetSite">The target site containing the resource group name, site and (optional) site name</param>
        /// <param name="appSettings">A <see cref="StringDictionary"/> containing the app settings to set</param>
        /// <returns>Awaitable <see cref="Task"/></returns>
        public static async Task PutAppSettingsAsync(ArmClient armClient, ServicePrincipalAccount servicePrincipalAccount, AppServiceConfigurationDictionary appSettings, TargetSite targetSite)
        {
            switch (targetSite.HasSlot)
            {
                case true:
                    await armClient.GetWebSiteSlotResource(WebSiteSlotResource.CreateResourceIdentifier(servicePrincipalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site, targetSite.Slot))
                                   .UpdateApplicationSettingsSlotAsync(appSettings);
                    break;
                case false:
                    await armClient.GetWebSiteResource(WebSiteResource.CreateResourceIdentifier(servicePrincipalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site))
                                   .UpdateApplicationSettingsAsync(appSettings);
                    break;
            }
        }

        /// <summary>
        /// Puts (overwrite) List of setting names who's values should be sticky (slot settings).
        /// </summary>
        /// <param name="webAppClient">The <see cref="WebSiteManagementClient"/> that will be used to submit the new list</param>
        /// <param name="targetSite">The target site containing the resource group name, site and (optional) site name</param>
        /// <param name="slotConfigNames">collection of setting names to be marked as sticky (slot setting)</param>
        /// <param name="authToken">The authorization token used to authenticate the request</param>
        /// <returns>Awaitable <see cref="Task"/></returns>
        public static async Task PutSlotSettingsListAsync(ArmClient armClient,
                                                          ServicePrincipalAccount servicePrincipalAccount,
                                                          TargetSite targetSite,
                                                          IEnumerable<string> slotConfigNames)
        {
            var data = new SlotConfigNamesResourceData();
            data.AppSettingNames.AddRange(slotConfigNames);

            await armClient.GetWebSiteResource(WebSiteResource.CreateResourceIdentifier(servicePrincipalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site))
                           .GetSlotConfigNamesResource()
                           .CreateOrUpdateAsync(WaitUntil.Completed, data);
        }

        /// <summary>
        /// Gets list of existing sticky (slot settings)
        /// </summary>
        /// <param name="webAppClient">The <see cref="WebSiteManagementClient"/> that will be used to submit the get request</param>
        /// <param name="targetSite">The <see cref="TargetSite"/> that will represents the web app's resource group, name and (optionally) slot that is being deployed to</param>
        /// <returns>Collection of setting names that are sticky (slot setting)</returns>
        public static async Task<IEnumerable<string>> GetSlotSettingsListAsync(ArmClient armClient, ServicePrincipalAccount servicePrincipalAccount, TargetSite targetSite)
        {
            SlotConfigNamesResource configNamesResource = await armClient.GetWebSiteResource(WebSiteResource.CreateResourceIdentifier(servicePrincipalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site))
                                                                         .GetSlotConfigNamesResource()
                                                                         .GetAsync();

            return configNamesResource.Data.AppSettingNames;
        }

        public static async Task<ConnectionStringDictionary> GetConnectionStringsAsync(ArmClient armClient, ServicePrincipalAccount servicePrincipalAccount, TargetSite targetSite)
        {
            return targetSite.HasSlot switch
                   {
                       true => await armClient.GetWebSiteSlotResource(WebSiteSlotResource.CreateResourceIdentifier(servicePrincipalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site, targetSite.Slot))
                                              .GetConnectionStringsSlotAsync(),
                       false => await armClient.GetWebSiteResource(WebSiteResource.CreateResourceIdentifier(servicePrincipalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site))
                                               .GetConnectionStringsAsync()
                   };
        }
        
        public static async Task PutConnectionStringsAsync(ArmClient armClient, ServicePrincipalAccount servicePrincipalAccount, ConnectionStringDictionary connectionStrings, TargetSite targetSite)
        {
            switch (targetSite.HasSlot)
            {
                case true:
                    await armClient.GetWebSiteSlotResource(WebSiteSlotResource.CreateResourceIdentifier(servicePrincipalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site, targetSite.Slot))
                                   .UpdateConnectionStringsSlotAsync(connectionStrings);
                    break;
                case false:
                    await armClient.GetWebSiteResource(WebSiteResource.CreateResourceIdentifier(servicePrincipalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site))
                                   .UpdateConnectionStringsAsync(connectionStrings);
                    break;
            }
        }
    }
}
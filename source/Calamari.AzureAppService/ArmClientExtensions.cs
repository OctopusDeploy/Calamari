using System.Collections.Generic;
using System.IO;
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
    ///<summary>
    /// Provides a set of static methods for interacting with an <see cref="ArmClient"/> using an <see cref="AzureTargetSite"/>.
    ///</summary>
    /// <remarks>
    /// These methods are suffixed with <i>Async</i> for consistency with the <see cref="ArmClient"/>.
    /// In the <b>Azure.ResourceManager</b> SDKs, <i>Async</i>-suffixed methods indicate an API call is made to Azure.
    /// </remarks>
    public static class ArmClientExtensions
    {
        public static async Task<SiteConfigData> GetSiteConfigDataAsync(this ArmClient armClient, AzureTargetSite targetSite)
        {
            return targetSite.HasSlot switch
                   {
                       true => (await armClient.GetWebSiteSlotConfigResource(targetSite.CreateResourceIdentifier())
                                               .GetAsync()).Value.Data,
                       false => (await armClient.GetWebSiteConfigResource(targetSite.CreateResourceIdentifier())
                                                .GetAsync()).Value.Data
                   };
        }

        public static async Task UpdateSiteConfigDataAsync(this ArmClient armClient, AzureTargetSite targetSite, SiteConfigData siteConfigData)
        {
            switch (targetSite.HasSlot)
            {
                case true:
                    await armClient.GetWebSiteSlotConfigResource(targetSite.CreateResourceIdentifier())
                                   .UpdateAsync(siteConfigData);
                    break;
                case false:
                    await armClient.GetWebSiteConfigResource(targetSite.CreateResourceIdentifier())
                                   .UpdateAsync(siteConfigData);
                    break;
            }
        }

        static readonly CsmPublishingProfile PublishingProfileOptions = new CsmPublishingProfile { Format = PublishingProfileFormat.WebDeploy };

        public static async Task<Stream> GetPublishingProfileXmlWithSecrets(this ArmClient armClient, AzureTargetSite targetSite)
        {
            return targetSite.HasSlot switch
                   {
                       true => await armClient.GetWebSiteSlotResource(targetSite.CreateResourceIdentifier())
                                              .GetPublishingProfileXmlWithSecretsSlotAsync(PublishingProfileOptions),
                       false => await armClient.GetWebSiteResource(targetSite.CreateResourceIdentifier())
                                               .GetPublishingProfileXmlWithSecretsAsync(PublishingProfileOptions)
                   };
        }

        public static async Task<AppServiceConfigurationDictionary> GetAppSettingsAsync(this ArmClient armClient, AzureTargetSite targetSite)
        {
            return targetSite.HasSlot switch
                   {
                       true => await armClient.GetWebSiteSlotResource(targetSite.CreateResourceIdentifier())
                                              .GetApplicationSettingsSlotAsync(),
                       false => await armClient.GetWebSiteResource(targetSite.CreateResourceIdentifier())
                                               .GetApplicationSettingsAsync()
                   };
        }

        /// <summary>
        /// Patches (add or update) the app settings for a web app or slot using the website management client library extensions.
        /// If any setting needs to be marked sticky (slot setting), update it via <see cref="PutSlotSettingsListAsync"/>.
        /// </summary>
        /// <param name="armClient">A <see cref="ArmClient"/> that is directly used to update app settings.</param>
        /// <param name="targetSite">The target site containing the resource group name, site and (optional) site name</param>
        /// <param name="appSettings">A <see cref="AppServiceConfigurationDictionary"/> containing the app settings to set</param>
        /// <returns>Awaitable <see cref="Task"/></returns>
        public static async Task UpdateAppSettingsAsync(this ArmClient armClient, AzureTargetSite targetSite, AppServiceConfigurationDictionary appSettings)
        {
            switch (targetSite.HasSlot)
            {
                case true:
                    await armClient.GetWebSiteSlotResource(targetSite.CreateResourceIdentifier()).UpdateApplicationSettingsSlotAsync(appSettings);
                    break;
                case false:
                    await armClient.GetWebSiteResource(targetSite.CreateResourceIdentifier()).UpdateApplicationSettingsAsync(appSettings);
                    break;
            }
        }

        public static async Task<IEnumerable<AppSetting>> GetAppSettingsListAsync(this ArmClient armClient, AzureTargetSite targetSite)
        {
            var appSettings = await GetAppSettingsAsync(armClient, targetSite);

            var slotSettings = await GetSlotSettingsAsync(armClient, targetSite);
            var slotSettingsLookup = slotSettings.ToHashSet();

            return appSettings.Properties.Select(
                                                 setting =>
                                                     new AppSetting
                                                     {
                                                         Name = setting.Key,
                                                         Value = setting.Value,
                                                         SlotSetting = slotSettingsLookup.Contains(setting.Key)
                                                     })
                              .ToList();
        }

        /// <summary>
        /// Gets list of existing sticky (slot settings)
        /// </summary>
        /// <param name="armClient">The <see cref="ArmClient"/> that will be used to submit the get request</param>
        /// <param name="targetSite">The <see cref="AzureTargetSite"/> that will represents the web app's resource group, name and (optionally) slot that is being deployed to</param>
        /// <returns>Collection of setting names that are sticky (slot setting)</returns>
        public static async Task<IEnumerable<string>> GetSlotSettingsAsync(this ArmClient armClient, AzureTargetSite targetSite)
        {
            SlotConfigNamesResource configNamesResource = await armClient.GetWebSiteResource(targetSite.CreateWebSiteResourceIdentifier())
                                                                         .GetSlotConfigNamesResource()
                                                                         .GetAsync();

            return configNamesResource.Data.AppSettingNames;
        }

        /// <summary>
        /// Puts (overwrite) List of setting names who's values should be sticky (slot settings).
        /// </summary>
        /// <param name="armClient">The <see cref="ArmClient"/> that will be used to submit the new list</param>
        /// <param name="targetSite">The target site containing the resource group name, site and (optional) site name</param>
        /// <param name="slotConfigNames">collection of setting names to be marked as sticky (slot setting)</param>
        /// <returns>Awaitable <see cref="Task"/></returns>
        public static async Task UpdateSlotSettingsAsync(this ArmClient armClient,
                                                         AzureTargetSite targetSite,
                                                         IEnumerable<string> slotConfigNames)
        {
            var data = new SlotConfigNamesResourceData();
            data.AppSettingNames.AddRange(slotConfigNames);

            await armClient.GetWebSiteResource(targetSite.CreateWebSiteResourceIdentifier())
                           .GetSlotConfigNamesResource()
                           .CreateOrUpdateAsync(WaitUntil.Completed, data);
        }

        public static async Task<ConnectionStringDictionary> GetConnectionStringsAsync(this ArmClient armClient, AzureTargetSite targetSite)
        {
            return targetSite.HasSlot switch
                   {
                       true => await armClient.GetWebSiteSlotResource(targetSite.CreateResourceIdentifier())
                                              .GetConnectionStringsSlotAsync(),
                       false => await armClient.GetWebSiteResource(targetSite.CreateResourceIdentifier())
                                               .GetConnectionStringsAsync()
                   };
        }

        public static async Task UpdateConnectionStringsAsync(this ArmClient armClient, AzureTargetSite targetSite, ConnectionStringDictionary connectionStrings)
        {
            switch (targetSite.HasSlot)
            {
                case true:
                    await armClient.GetWebSiteSlotResource(targetSite.CreateResourceIdentifier()).UpdateConnectionStringsSlotAsync(connectionStrings);
                    break;
                case false:
                    await armClient.GetWebSiteResource(targetSite.CreateResourceIdentifier()).UpdateConnectionStringsAsync(connectionStrings);
                    break;
            }
        }

        public static async Task RestartWebSiteAsync(this ArmClient armClient, AzureTargetSite targetSite)
        {
            switch (targetSite.HasSlot)
            {
                case true:
                    await armClient.GetWebSiteSlotResource(targetSite.CreateResourceIdentifier())
                                   .RestartSlotAsync();
                    break;
                case false:
                    await armClient.GetWebSiteResource(targetSite.CreateResourceIdentifier())
                                   .RestartAsync();
                    break;
            }
        }
    }
}
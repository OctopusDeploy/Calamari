#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.AzureAppService.Json;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using Newtonsoft.Json;

namespace Calamari.AzureAppService
{
    public static class AppSettingsManagement
    {
        public static async Task<IEnumerable<AppSetting>> GetAppSettingsAsync(WebSiteManagementClient webAppClient,
            string authToken, TargetSite targetSite)
        {
            var webAppSettings = await webAppClient.WebApps.ListApplicationSettingsAsync(targetSite);

            var slotSettings = (await GetSlotSettingsListAsync(webAppClient, authToken, targetSite)).ToArray();

            return webAppSettings.Properties.Select(
                setting =>
                    new AppSetting
                    {
                        Name = setting.Key,
                        Value = setting.Value,
                        SlotSetting = slotSettings.Any(x => x == setting.Key)
                    }).ToList();
        }

        /// <summary>
        /// Patches (add or update) the app settings for a web app or slot using the website management client library extensions.
        /// If any setting needs to be marked sticky (slot setting), update it via <see cref="PutSlotSettingsListAsync"/>.
        /// </summary>
        /// <param name="webAppClient">A <see cref="WebSiteManagementClient"/> that is directly used to update app settings.<seealso cref="WebSiteManagementClientExtensions"/></param>
        /// <param name="targetSite">The target site containing the resource group name, site and (optional) site name</param>
        /// <param name="appSettings">A <see cref="StringDictionary"/> containing the app settings to set</param>
        /// <returns>Awaitable <see cref="Task"/></returns>
        public static async Task PutAppSettingsAsync(WebSiteManagementClient webAppClient, StringDictionary appSettings,
            TargetSite targetSite)
        {
            await webAppClient.WebApps.UpdateApplicationSettingsAsync(targetSite, appSettings);
        }

        /// <summary>
        /// Puts (overwrite) List of setting names who's values should be sticky (slot settings).
        /// </summary>
        /// <param name="webAppClient">The <see cref="WebSiteManagementClient"/> that will be used to submit the new list</param>
        /// <param name="targetSite">The target site containing the resource group name, site and (optional) site name</param>
        /// <param name="slotConfigNames">collection of setting names to be marked as sticky (slot setting)</param>
        /// <param name="authToken">The authorization token used to authenticate the request</param>
        /// <returns>Awaitable <see cref="Task"/></returns>
        public static async Task PutSlotSettingsListAsync(WebSiteManagementClient webAppClient, TargetSite targetSite,
            IEnumerable<string> slotConfigNames, string authToken)
        {
            var client = webAppClient.HttpClient;
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var targetUrl =
                $"https://management.azure.com/subscriptions/{webAppClient.SubscriptionId}/resourceGroups/{targetSite.ResourceGroupName}/providers/Microsoft.Web/sites/{targetSite.Site}/config/slotconfignames?api-version=2018-11-01";

            var slotSettingsJson = new appSettingNamesRoot
                {properties = new properties {appSettingNames = slotConfigNames}, name = targetSite.Site};
            var postBody = JsonConvert.SerializeObject(slotSettingsJson);

            //var body = new StringContent(postBody);
            var body = new StringContent(postBody, Encoding.UTF8, "application/json");
            var response = await client.PutAsync(targetUrl, body);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }
        }

        /// <summary>
        /// Gets list of existing sticky (slot settings)
        /// </summary>
        /// <param name="webAppClient">The <see cref="WebSiteManagementClient"/> that will be used to submit the get request</param>
        /// <param name="targetSite">The <see cref="TargetSite"/> that will represents the web app's resource group, name and (optionally) slot that is being deployed to</param>
        /// <returns>Collection of setting names that are sticky (slot setting)</returns>
        public static async Task<IEnumerable<string>> GetSlotSettingsListAsync(WebSiteManagementClient webAppClient,
            string authToken, TargetSite targetSite)
        {
            var client = webAppClient.HttpClient;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            var targetUrl =
                $"{client.BaseAddress}/subscriptions/{webAppClient.SubscriptionId}/resourceGroups/{targetSite.ResourceGroupName}/providers/Microsoft.Web/sites/{targetSite.Site}/config/slotconfignames?api-version=2018-11-01";

            var results = await client.GetStringAsync(targetUrl);

            var output = JsonConvert.DeserializeObject<appSettingNamesRoot>(results).properties.appSettingNames ??
                         new List<string>();

            return output;
        }

        public static async Task<ConnectionStringDictionary> GetConnectionStringsAsync(
            WebSiteManagementClient webAppClient, TargetSite targetSite)
        {
            var conStringDict = await webAppClient.WebApps.ListConnectionStringsAsync(targetSite);

            return conStringDict;
        }
    }
}

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureAppService.Json;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using Newtonsoft.Json;

namespace Calamari.AzureAppService
{
    class AppSettingsManagement
    {
        /// <summary>
        /// Patches (add or update) the app settings for a web app or slot using the website management client library extensions.
        /// If any setting needs to be marked sticky (slot setting), update it via <see cref="PutSlotSettingsListAsync"/>.
        /// </summary>
        /// <param name="webAppClient">A <see cref="WebSiteManagementClient"/> that is directly used to update app settings.<seealso cref="WebSiteManagementClientExtensions"/></param>
        /// <param name="resourceGroupName">The name of the resource group that houses the webapp</param>
        /// <param name="appName">The name of the webapp being updated</param>
        /// <param name="appSettings">A <see cref="StringDictionary"/> containing the app settings to set</param>
        /// <param name="slotName">The slot name of the app being updated.  Leave blank to specify the production slot. Defaults to null.</param>
        /// <returns>Awaitable <see cref="Task"/></returns>
        public static async Task PatchAppSettingsAsync(WebSiteManagementClient webAppClient, string resourceGroupName, string appName, StringDictionary appSettings, string? slotName=null)//HttpClient client)
        {
            _ = !string.IsNullOrEmpty(slotName)
                ? await webAppClient.WebApps.UpdateApplicationSettingsAsync(resourceGroupName, appName, appSettings)
                : await webAppClient.WebApps.UpdateApplicationSettingsSlotAsync(resourceGroupName, appName, appSettings,
                    slotName);
        }

        /// <summary>
        /// Puts (overwrite) List of setting names who's values should be sticky (slot settings).
        /// </summary>
        /// <param name="webAppClient">The <see cref="WebSiteManagementClient"/> that will be used to submit the new list</param>
        /// <param name="resourceGroupName">The name of the resource group housing the webapp being updated</param>
        /// <param name="appName">The name of the web app being updated</param>
        /// <param name="slotConfigNames">collection of setting names to be marked as sticky (slot setting)</param>
        /// <param name="authToken">The authorization token used to authenticate the request</param>
        /// <returns>Awaitable <see cref="Task"/></returns>
        public static async Task PutSlotSettingsListAsync(WebSiteManagementClient webAppClient, string resourceGroupName,
            string appName, IEnumerable<string> slotConfigNames, string authToken)
        {
            var client = webAppClient.HttpClient;
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var targetUrl =
                $"https://management.azure.com/subscriptions/{webAppClient.SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{appName}/config/slotconfignames?api-version=2018-11-01";

            var slotSettingsJson = new appSettingNamesRoot
                {properties = new properties {appSettingNames = slotConfigNames}, name = appName};
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
        /// <param name="resourceGroupName">The name of the resource group housing the webapp</param>
        /// <param name="appName">The name of the webapp being updated</param>
        /// <param name="authToken">The authorization token used to authenticate the request</param>
        /// <returns>Collection of setting names that are sticky (slot setting)</returns>
        public static async Task<IEnumerable<string>> GetSlotSettingsListAsync(WebSiteManagementClient webAppClient,
            string resourceGroupName, string appName, string authToken)
        {
            var client = webAppClient.HttpClient;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            var targetUrl =
                $"https://management.azure.com/subscriptions/{webAppClient.SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{appName}/config/slotconfignames?api-version=2018-11-01";

            var results = await client.GetStringAsync(targetUrl);

            var output = JsonConvert.DeserializeObject<appSettingNamesRoot>(results).properties.appSettingNames ??
                         new List<string>();

            return output;
        }
    }
}

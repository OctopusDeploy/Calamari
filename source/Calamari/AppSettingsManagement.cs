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
        public static async Task PatchAppSettings(WebSiteManagementClient webAppClient, string resourceGroupName, string appName, StringDictionary appSettings, string? slotName=null)//HttpClient client)
        {
            _ = slotName == null
                ? await webAppClient.WebApps.UpdateApplicationSettingsAsync(resourceGroupName, appName, appSettings)
                : await webAppClient.WebApps.UpdateApplicationSettingsSlotAsync(resourceGroupName, appName, appSettings,
                    slotName);
        }

        public static async Task PutSlotSettingsList(WebSiteManagementClient webAppClient, string resourceGroupName,
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
        }

        public static async Task<IEnumerable<string>> GetSlotSettingsList(WebSiteManagementClient webAppClient,
            string resourceGroupName, string appName, string authToken)
        {
            var client = webAppClient.HttpClient;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            var targetUrl =
                $"https://management.azure.com/subscriptions/{webAppClient.SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{appName}/config/slotconfignames?api-version=2018-11-01";

            var output = JsonConvert.DeserializeObject<appSettingNamesRoot>(await client.GetStringAsync(targetUrl)).properties.appSettingNames;
            return output;
        }
    }
}

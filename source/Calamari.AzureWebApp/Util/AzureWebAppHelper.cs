using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Calamari.AzureWebApp.Integration.Websites.Publishing;
using Newtonsoft.Json.Linq;

namespace Calamari.AzureWebApp.Util
{
    static class AzureWebAppHelper
    {
        public static async Task<bool> GetBasicPublishingCredentialsPoliciesAsync(string resourceManagementEndpointBaseUri , string subscriptionId, string resourceGroupName, string webAppName, string accessToken)
        {
            var url = $"{resourceManagementEndpointBaseUri.TrimEnd('/')}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{webAppName}/basicPublishingCredentialsPolicies/scm?api-version=2021-01-15";
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var jObject = JObject.Parse(json);
                return jObject["properties"]["allow"].Value<bool>();
            }
        }
    }
}
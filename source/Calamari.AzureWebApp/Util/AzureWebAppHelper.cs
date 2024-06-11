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
        public static AzureTargetSite GetAzureTargetSite(string siteAndMaybeSlotName, string slotName)
        {
            var targetSite = new AzureTargetSite {RawSite = siteAndMaybeSlotName};

            if (siteAndMaybeSlotName.Contains("("))
            {
                // legacy site and slot "site(slot)"
                var parenthesesIndex = siteAndMaybeSlotName.IndexOf("(", StringComparison.Ordinal);
                targetSite.Site = siteAndMaybeSlotName.Substring(0, parenthesesIndex).Trim();
                targetSite.Slot = siteAndMaybeSlotName.Substring(parenthesesIndex + 1).Replace(")", string.Empty).Trim();
                return targetSite;
            }

            if (siteAndMaybeSlotName.Contains("/"))
            {
                // "site/slot"
                var slashIndex = siteAndMaybeSlotName.IndexOf("/", StringComparison.Ordinal);
                targetSite.Site = siteAndMaybeSlotName.Substring(0, slashIndex).Trim();
                targetSite.Slot = siteAndMaybeSlotName.Substring(slashIndex + 1).Trim();
                return targetSite;
            }

            targetSite.Site = siteAndMaybeSlotName;
            targetSite.Slot = slotName;
            return targetSite;
        }
       
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
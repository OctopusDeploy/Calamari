using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Azure.Integration.Security;
using Calamari.Azure.Util;
using Calamari.Commands.Support;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Rest;
using Microsoft.Rest.Azure.OData;
using Newtonsoft.Json.Linq;

namespace Calamari.Azure.Integration.Websites.Publishing
{
    public class ResourceManagerPublishProfileProvider
    {
        public static SitePublishProfile GetPublishProperties(string subscriptionId, string resourceGroupName, AzureTargetSite azureTargetSite, string tenantId, string applicationId, string password,string resourceManagementEndpoint, string activeDirectoryEndPoint)
        {
            var token = ServicePrincipal.GetAuthorizationToken(tenantId, applicationId, password, resourceManagementEndpoint, activeDirectoryEndPoint);
            var baseUri = new Uri(resourceManagementEndpoint);
            using (var resourcesClient = new ResourceManagementClient(new TokenCredentials(token))
			{
                SubscriptionId = subscriptionId,
                BaseUri = baseUri,
            })
            using (var webSiteClient = new WebSiteManagementClient(new Uri(resourceManagementEndpoint), new TokenCredentials(token)) { SubscriptionId = subscriptionId})
            {
                resourcesClient.HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                resourcesClient.HttpClient.BaseAddress = baseUri;

                Log.Verbose($"Looking up site {azureTargetSite.Site} in resourceGroup {resourceGroupName}");

                var sites = webSiteClient.WebApps.List();
                if (sites.Any())
                {
                    Log.Verbose("Found sites:");
                    foreach (var site in sites)
                    {
                        Log.Verbose($"{site.ResourceGroup} / {site.Name}");
                    }
                }

                var matchingSites = sites
                    .Where(webApp => string.Equals(webApp.Name, azureTargetSite.Site, StringComparison.CurrentCultureIgnoreCase) &&
                                         (string.IsNullOrWhiteSpace(resourceGroupName) || string.Equals(webApp.ResourceGroup, resourceGroupName, StringComparison.InvariantCultureIgnoreCase)))
                                         .ToList();

                if (!matchingSites.Any())
                {
                    var resourceGroupMessage = !string.IsNullOrWhiteSpace(resourceGroupName)
                        ? $" in resource group '{resourceGroupName}' and"
                        : " in";
                    throw new CommandException($"Could not find Azure WebSite '{azureTargetSite.Site}'{resourceGroupMessage} subscription '{subscriptionId}'");
                }
                    
                // if more than one site, fail
                if (matchingSites.Count > 1)
                    throw new CommandException(
                        $"Found {matchingSites.Count} matching the site name '{azureTargetSite.Site}' in subscription '{subscriptionId}'.{(string.IsNullOrWhiteSpace(resourceGroupName) ? " Please supply a Resource Group name." : string.Empty)}");

                var matchingSite = matchingSites.Single();
                resourceGroupName = matchingSite.ResourceGroup;

                // ARM resource ID of the source app. App resource ID is of the form:
                //  - /subscriptions/{subId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{siteName} for production slots and
                //  - /subscriptions/{subId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{siteName}/slots/{slotName} for other slots.

                // We allow the slot to be defined on both the target directly (which will come through on the matchingSite.Name) or on the 
                // step for backwards compatibility with older Azure steps.
                var siteAndSlotPath = matchingSite.Name;
                if (azureTargetSite.HasSlot)
                {
                    Log.Verbose($"Using the deployment slot {azureTargetSite.Slot}");
                    siteAndSlotPath = $"{matchingSite.Name}/slots/{azureTargetSite.Slot}";
                }
                
                // Once we know the Resource Group, we have to POST a request to the URI below to retrieve the publishing credentials
                var publishSettingsUri = new Uri(resourcesClient.BaseUri,
                    $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{siteAndSlotPath}/config/publishingCredentials/list?api-version=2016-08-01");
                Log.Verbose($"Retrieving publishing profile from {publishSettingsUri}");

                SitePublishProfile publishProperties = null;
                var request = new HttpRequestMessage { Method = HttpMethod.Post, RequestUri = publishSettingsUri };
                // Add the authentication headers
                var requestTask = resourcesClient.Credentials.ProcessHttpRequestAsync(request, new CancellationToken())
                    .ContinueWith(authResult => resourcesClient.HttpClient.SendAsync(request), TaskContinuationOptions.NotOnFaulted)
                    .ContinueWith(publishSettingsResponse =>
                    {
                        var result = publishSettingsResponse.Result.Result;
                        if (!result.IsSuccessStatusCode)
                        {
                            Log.Error($"Retrieving publishing credentials failed. Publish-settings URI: {publishSettingsUri}");
                            throw new Exception($"Retrieving publishing credentials failed with HTTP status {(int)result.StatusCode} - {result.ReasonPhrase}");
                        }

                        dynamic response = JObject.Parse(result.Content.AsString());
                        string publishUserName = response.properties.publishingUserName;
                        string publishPassword = response.properties.publishingPassword;
                        string scmUri = response.properties.scmUri;
                        Log.Verbose($"Retrieved publishing profile. URI: {scmUri}  UserName: {publishUserName}");
                        publishProperties = new SitePublishProfile(publishUserName, publishPassword, new Uri(scmUri));
                    }, TaskContinuationOptions.NotOnFaulted);

                requestTask.Wait();
                return publishProperties;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Azure.Accounts;
using Calamari.Azure.Integration.Security;
using Calamari.Azure.Util;
using Calamari.Commands.Support;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using Microsoft.Rest.TransientFaultHandling;
using Newtonsoft.Json.Linq;

namespace Calamari.Azure.Integration.Websites.Publishing
{
    public class ResourceManagerPublishProfileProvider
    {
        public static SitePublishProfile GetPublishProperties(AzureServicePrincipalAccount account, string resourceGroupName, AzureTargetSite azureTargetSite)
        {
            if (account.ResourceManagementEndpointBaseUri != DefaultVariables.ResourceManagementEndpoint)
                Log.Info("Using override for resource management endpoint - {0}", account.ResourceManagementEndpointBaseUri);

            if (account.ActiveDirectoryEndpointBaseUri != DefaultVariables.ActiveDirectoryEndpoint)
                Log.Info("Using override for Azure Active Directory endpoint - {0}", account.ActiveDirectoryEndpointBaseUri);

            var token = ServicePrincipal.GetAuthorizationToken(account.TenantId, account.ClientId, account.Password, account.ResourceManagementEndpointBaseUri, account.ActiveDirectoryEndpointBaseUri);
            var baseUri = new Uri(account.ResourceManagementEndpointBaseUri);
            using (var resourcesClient = new ResourceManagementClient(new TokenCredentials(token))
			{
                SubscriptionId = account.SubscriptionNumber,
                BaseUri = baseUri,
            })
            using (var webSiteClient = new WebSiteManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), new TokenCredentials(token)) { SubscriptionId = account.SubscriptionNumber })
            {
                webSiteClient.SetRetryPolicy(new Microsoft.Rest.TransientFaultHandling.RetryPolicy(new HttpStatusCodeErrorDetectionStrategy(), 3));
                resourcesClient.HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                resourcesClient.HttpClient.BaseAddress = baseUri;

                Log.Verbose($"Looking up site {azureTargetSite.Site} {(string.IsNullOrWhiteSpace(resourceGroupName) ? string.Empty : $"in resourceGroup {resourceGroupName}")}");

                Site matchingSite;
                if (string.IsNullOrWhiteSpace(resourceGroupName))
                {
                    matchingSite = FindSiteByNameWithRetry(account, azureTargetSite, webSiteClient) ?? throw new CommandException(GetSiteNotFoundExceptionMessage(account, azureTargetSite));
                }
                else
                {
                    var site = webSiteClient.WebApps.Get(resourceGroupName, azureTargetSite.Site);
                    Log.Verbose("Found site:");
                    LogSite(site);

                    matchingSite = site ?? throw new CommandException(GetSiteNotFoundExceptionMessage(account, azureTargetSite, resourceGroupName));
                }

                // ARM resource ID of the source app. App resource ID is of the form:
                //  - /subscriptions/{subId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{siteName} for production slots and
                //  - /subscriptions/{subId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{siteName}/slots/{slotName} for other slots.

                // We allow the slot to be defined on both the target directly (which will come through on the matchingSite.Name) or on the 
                // step for backwards compatibility with older Azure steps.
                resourceGroupName = matchingSite.ResourceGroup;
                var siteAndSlotPath = matchingSite.Name;
                if (azureTargetSite.HasSlot)
                {
                    Log.Verbose($"Using the deployment slot {azureTargetSite.Slot}");
                    siteAndSlotPath = $"{matchingSite.Name}/slots/{azureTargetSite.Slot}";
                }
                
                // Once we know the Resource Group, we have to POST a request to the URI below to retrieve the publishing credentials
                var publishSettingsUri = new Uri(resourcesClient.BaseUri,
                    $"/subscriptions/{account.SubscriptionNumber}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{siteAndSlotPath}/config/publishingCredentials/list?api-version=2016-08-01");
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

        private static Site FindSiteByNameWithRetry(AzureServicePrincipalAccount account, AzureTargetSite azureTargetSite,
            WebSiteManagementClient webSiteClient)
        {
            Site matchingSite = null;
            var retry = AzureRetryTracker.GetDefaultRetryTracker();
            while (retry.Try() && matchingSite == null)
            {
                var sites = webSiteClient.WebApps.List();
                var matchingSites = sites.Where(webApp =>
                    string.Equals(webApp.Name, azureTargetSite.Site, StringComparison.CurrentCultureIgnoreCase)).ToList();

                LogFoundSites(sites.ToList());

                if (!matchingSites.Any())
                {
                    throw new CommandException(GetSiteNotFoundExceptionMessage(account, azureTargetSite));
                }

                if (matchingSites.Count > 1)
                {
                    throw new CommandException(
                        $"Found {matchingSites.Count} matching the site name '{azureTargetSite.Site}' in subscription '{account.SubscriptionNumber}'. Please supply a Resource Group name.");
                }

                matchingSite = matchingSites.Single();

                // ensure the site loaded the resource group
                if (string.IsNullOrWhiteSpace(matchingSite.ResourceGroup))
                {
                    if (retry.CanRetry())
                    {
                        if (retry.ShouldLogWarning())
                        {
                            Log.Warn(
                                $"Azure Site query failed to return the resource group, trying again in {retry.Sleep()} ms.");
                        }

                        matchingSite = null;
                        Thread.Sleep(retry.Sleep());
                    }
                    else
                    {
                        throw new CommandException(GetSiteNotFoundExceptionMessage(account, azureTargetSite));
                    }
                }
            }

            return matchingSite;
        }

        private static string GetSiteNotFoundExceptionMessage(AzureServicePrincipalAccount account, AzureTargetSite azureTargetSite, string resourceGroupName = null)
        {
            bool hasResourceGroup = !string.IsNullOrWhiteSpace(resourceGroupName);
            StringBuilder sb = new StringBuilder($"Could not find Azure WebSite '{azureTargetSite.Site}'");
            sb.Append(hasResourceGroup ? $" in resource group '{resourceGroupName}'" : string.Empty);
            sb.Append($"in subscription '{account.SubscriptionNumber}'.");
            sb.Append(hasResourceGroup ? string.Empty : " Please supply a Resource Group name.");
            return sb.ToString();
        }

        private static void LogFoundSites(List<Site> sites)
        {
            if (sites.Any())
            {
                Log.Verbose("Found sites:");
                foreach (var site in sites)
                {
                    LogSite(site);
                }
            }
        }

        private static void LogSite(Site site)
        {
            if (site != null)
            {
                string resourceGroup = string.IsNullOrWhiteSpace(site.ResourceGroup)
                    ? string.Empty
                    : $"{site.ResourceGroup} / ";
                Log.Verbose($"\t{resourceGroup}{site.Name}");
            }
        }
    }
}
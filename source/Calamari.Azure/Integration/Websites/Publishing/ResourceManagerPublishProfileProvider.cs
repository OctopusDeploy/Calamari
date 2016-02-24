﻿using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Azure.Integration.Security;
using Calamari.Commands.Support;
using Microsoft.Azure;
using Microsoft.Azure.Management.Resources;
using Microsoft.Azure.Management.Resources.Models;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using Newtonsoft.Json.Linq;

namespace Calamari.Azure.Integration.Websites.Publishing
{
    public class ResourceManagerPublishProfileProvider
    {
        public static SitePublishProfile GetPublishProperties(string subscriptionId, string siteName, string tenantId, string applicationId, string password)
        {
            var token = ServicePrincipal.GetAuthorizationToken(tenantId, applicationId, password);
            using (var resourcesClient = new ResourceManagementClient(new TokenCloudCredentials(subscriptionId, token)))
            using (var webSiteClient = new WebSiteManagementClient(new TokenCredentials(token)) { SubscriptionId = subscriptionId})
            {
                // Because we only know the site name, we need to search the ResourceGroups to find it 
                var resourceGroups = resourcesClient.ResourceGroups.List(new ResourceGroupListParameters()).ResourceGroups.Select(rg => rg.Name).ToList();

                foreach (var resourceGroup in resourceGroups)
                {
                    var sites = webSiteClient.Sites.GetSites(resourceGroup, null, null, true).Value;
                    var matchingSite = sites.FirstOrDefault(x => x.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase));

                    if (matchingSite == null)
                        continue;

                    var siteNameToUse = GetCorrectSiteNameUrlPart(matchingSite);

                    // Once we know the Resource Group, we have to POST a request to the URI below to retrieve the publishing credentials
                    var publishSettingsUri = new Uri(resourcesClient.BaseUri, 
                        $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{siteNameToUse}/config/publishingCredentials/list?api-version=2015-08-01");
                    Log.Verbose($"Retrieving publishing profile from {publishSettingsUri}");

                    SitePublishProfile publishProperties = null;
                    var request = new HttpRequestMessage { Method = HttpMethod.Post, RequestUri = publishSettingsUri};
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

                throw new CommandException( $"Could not find Azure WebSite '{siteName}' in subscription '{subscriptionId}'"); 
            }
        }

        private static string GetCorrectSiteNameUrlPart(Resource matchingSite)
        {
            if (!matchingSite.Name.Contains("/")) return matchingSite.Name;
            if (matchingSite.Name.EndsWith("/")) return matchingSite.Name.Trim('/');  // I think it is safe to say you can't have a "/" in your site name

            var parts = matchingSite.Name.Split('/');

            // We will only use the first 2 parts as we are expecting the format "sitename/slotname
            return $"{parts[0]}/slots/{parts[1]}";
        }
    }
}
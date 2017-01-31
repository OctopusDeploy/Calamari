using System;
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
using Microsoft.Rest;
using Newtonsoft.Json.Linq;

namespace Calamari.Azure.Integration.Websites.Publishing
{
    public class ResourceManagerPublishProfileProvider
    {
        public static SitePublishProfile GetPublishProperties(string subscriptionId, string resourceGroupName, string siteName, string tenantId, string applicationId, string password,string resourceManagementEndpoint, string activeDirectoryEndPoint)
        {
            var token = ServicePrincipal.GetAuthorizationToken(tenantId, applicationId, password, resourceManagementEndpoint, activeDirectoryEndPoint);

            using (var resourcesClient = new ResourceManagementClient(new TokenCloudCredentials(subscriptionId, token), new Uri(resourceManagementEndpoint)))
            using (var webSiteClient = new WebSiteManagementClient(new Uri(resourceManagementEndpoint), new TokenCredentials(token)) { SubscriptionId = subscriptionId})
            {
                // We may need to search all ResourceGroups, if one isn't specified.  New Step template will always provide the Resource Group, it is currently treated as optional here
                // for backward compatibility.
                var resourceGroups = resourcesClient.ResourceGroups.List(new ResourceGroupListParameters()).ResourceGroups.Where(rg => string.IsNullOrWhiteSpace(resourceGroupName) || string.Equals(rg.Name, resourceGroupName, StringComparison.InvariantCultureIgnoreCase)).Select(rg => rg.Name).ToList();

                foreach (var resourceGroup in resourceGroups)
                {
                    var sites = webSiteClient.Sites.GetSites(resourceGroup, null, null, true).Value;
                    var matchingSite = sites.FirstOrDefault(x => x.SiteName.Equals(siteName, StringComparison.OrdinalIgnoreCase));

                    if (matchingSite == null)
                        continue;

                    // Once we know the Resource Group, we have to POST a request to the URI below to retrieve the publishing credentials
                    var publishSettingsUri = new Uri(resourcesClient.BaseUri,
                        $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{matchingSite.Name.Replace("/", "/slots/")}/config/publishingCredentials/list?api-version=2015-08-01");
                    Log.Verbose($"Retrieving publishing profile from {publishSettingsUri}");

                    SitePublishProfile publishProperties = null;
                    var request = new HttpRequestMessage {Method = HttpMethod.Post, RequestUri = publishSettingsUri};
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

                throw new CommandException(
                    $"Could not find Azure WebSite '{siteName}' in subscription '{subscriptionId}'");
            }
        }
    }
}
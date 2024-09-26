using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.Azure.AppServices;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AzureWebApp.Integration.Websites.Publishing
{
    class ResourceManagerPublishProfileProvider
    {
        readonly ILog log;

        public ResourceManagerPublishProfileProvider(ILog log)
        {
            this.log = log;
        }

        public async Task<WebDeployPublishSettings> GetPublishProperties(IAzureAccount account, AzureTargetSite azureTargetSite)
        {
            var armClient = account.CreateArmClient();

            log.Verbose($"Looking up site {azureTargetSite.Site} {(string.IsNullOrWhiteSpace(azureTargetSite.ResourceGroupName) ? string.Empty : $"in resourceGroup {azureTargetSite.ResourceGroupName}")}");
            
            //if we don't have a resource group, we need to find the web app and update the target site with the resource group info
            if (string.IsNullOrWhiteSpace(azureTargetSite.ResourceGroupName))
            {
                azureTargetSite = await FindTargetSiteResourceGroup(armClient, azureTargetSite);
            }

            // We allow the slot to be defined on both the target directly (which will come through on the matchingSite.Name) or on the
            // step for backwards compatibility with older Azure steps.
            if (azureTargetSite.HasSlot)
            {
                log.Verbose($"Using the deployment slot {azureTargetSite.Slot}");
            }

            return await GetWebDeployPublishProfile(armClient, azureTargetSite);
        }

        static async Task<WebDeployPublishSettings> GetWebDeployPublishProfile(ArmClient armClient, AzureTargetSite targetSite)
        {
            var stream = await armClient.GetPublishingProfileXmlWithSecrets(targetSite);

            string text;
            using (var streamReader = new StreamReader(stream))
            {
                text = await streamReader.ReadToEndAsync();
            }

            var document = XDocument.Parse(text);

            var profile = (from el in document.Descendants("publishProfile")
                           where string.Compare(el.Attribute("publishMethod")?.Value, "MSDeploy", StringComparison.OrdinalIgnoreCase) == 0
                           select new
                           {
                               PublishUrl = $"https://{el.Attribute("publishUrl")?.Value}",
                               Username = el.Attribute("userName")?.Value,
                               Password = el.Attribute("userPWD")?.Value,
                               Site = el.Attribute("msdeploySite")?.Value
                           }).FirstOrDefault();

            if (profile == null)
            {
                throw new Exception("Failed to retrieve publishing profile.");
            }

            return new WebDeployPublishSettings(profile.Site, new SitePublishProfile(profile.Username, profile.Password, new Uri(profile.PublishUrl)));
        }

        async Task<AzureTargetSite> FindTargetSiteResourceGroup(ArmClient armClient, AzureTargetSite azureTargetSite)
        {
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(azureTargetSite.SubscriptionId));

            var webSitesEnumerator = subscription.GetWebSitesAsync().GetAsyncEnumerator();

            var matchingWebSiteResources = new List<WebSiteResource>();
            while (await webSitesEnumerator.MoveNextAsync())
            {
                var webSiteResource = webSitesEnumerator.Current;

                if (string.Equals(webSiteResource.Data.Name, azureTargetSite.Site, StringComparison.OrdinalIgnoreCase))
                {
                    matchingWebSiteResources.Add(webSiteResource);
                }
            }

            LogFoundSites(matchingWebSiteResources);

            if (!matchingWebSiteResources.Any())
            {
                throw new CommandException(GetSiteNotFoundExceptionMessage(azureTargetSite));
            }

            if (matchingWebSiteResources.Count > 1)
            {
                throw new CommandException($"Found {matchingWebSiteResources.Count} matching the site name '{azureTargetSite.Site}' in subscription '{subscription.Id}'. Please supply a Resource Group name.");
            }

            var siteResource = matchingWebSiteResources.Single();

            return new AzureTargetSite(azureTargetSite.SubscriptionId,
                                       siteResource.Data.ResourceGroup,
                                       siteResource.Data.Name,
                                       azureTargetSite.Slot);
        }

        static string GetSiteNotFoundExceptionMessage(AzureTargetSite azureTargetSite, string resourceGroupName = null)
        {
            var hasResourceGroup = !string.IsNullOrWhiteSpace(resourceGroupName);
            var sb = new StringBuilder($"Could not find Azure WebSite '{azureTargetSite.Site}'");
            sb.Append(hasResourceGroup ? $" in resource group '{resourceGroupName}'" : string.Empty);
            sb.Append($" in subscription '{azureTargetSite.SubscriptionId}'.");
            sb.Append(hasResourceGroup ? string.Empty : " Please supply a Resource Group name.");
            return sb.ToString();
        }

        void LogFoundSites(List<WebSiteResource> sites)
        {
            if (sites.Any())
            {
                log.Verbose("Found sites:");
                foreach (var site in sites)
                {
                    log.Verbose($"\t{site.Data.ResourceGroup} / {site.Data.Name}");
                }
            }
        }
    }
}
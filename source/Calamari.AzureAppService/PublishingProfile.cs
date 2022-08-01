using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calamari.Azure;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Rest;

namespace Calamari.AzureAppService
{
    internal class PublishingProfile
    {
        public string Site { get; set; }

        public string Password { get; set; }

        public string Username { get; set; }

        public string PublishUrl { get; set; }
        
        public static async Task<PublishingProfile> GetPublishingProfile(TargetSite targetSite,
            ServicePrincipalAccount account)
        {
            string mgmtEndpoint = account.ResourceManagementEndpointBaseUri;
            var token = new TokenCredentials(await Auth.GetAuthTokenAsync(account));

            var azureCredentials = new AzureCredentials(
                    token,
                    token,
                    account.TenantId,
                    new AzureKnownEnvironment(account.AzureEnvironment).AsAzureSDKEnvironment())
                .WithDefaultSubscription(account.SubscriptionNumber);

            var restClient = RestClient
                .Configure()
                .WithBaseUri(mgmtEndpoint)
                .WithEnvironment(new AzureKnownEnvironment(account.AzureEnvironment).AsAzureSDKEnvironment())
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .WithCredentials(azureCredentials)
                .Build();

            var webAppClient = new WebSiteManagementClient(restClient)
                {SubscriptionId = account.SubscriptionNumber};

            var options = new CsmPublishingProfileOptions {Format = PublishingProfileFormat.WebDeploy};

            await webAppClient.WebApps.GetWithHttpMessagesAsync(targetSite.ResourceGroupName, targetSite.Site);

            using var publishProfileStream = targetSite.HasSlot
                ? await webAppClient.WebApps.ListPublishingProfileXmlWithSecretsSlotAsync(targetSite.ResourceGroupName,
                    targetSite.Site, options, targetSite.Slot)
                : await webAppClient.WebApps.ListPublishingProfileXmlWithSecretsAsync(targetSite.ResourceGroupName,
                    targetSite.Site,
                    options);

            using var streamReader = new StreamReader(publishProfileStream);
            var document = XDocument.Parse(await streamReader.ReadToEndAsync());

            var profile = (from el in document.Descendants("publishProfile")
                where string.Compare(el.Attribute("publishMethod")?.Value, "MSDeploy",
                    StringComparison.OrdinalIgnoreCase) == 0
                select new PublishingProfile
                {
                    PublishUrl = $"https://{el.Attribute("publishUrl")?.Value}",
                    Username = el.Attribute("userName")?.Value,
                    Password = el.Attribute("userPWD")?.Value,
                    Site = el.Attribute("msdeploySite")?.Value
                }).FirstOrDefault();

            if (profile == null) throw new Exception("Failed to retrieve publishing profile.");

            return profile;
        }
    }
}

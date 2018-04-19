using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Calamari.Commands.Support;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.WebSites.Models;

namespace Calamari.Azure.Integration.Websites.Publishing
{
    public class ServiceManagementPublishProfileProvider  
    {
        public static SitePublishProfile GetPublishProperties(string subscriptionId, byte[] certificateBytes, string siteAndSlotName, string serviceManagementEndpoint)
        {
            Log.Verbose($"Service Management endpoint is {serviceManagementEndpoint}");
            Log.Verbose($"Retrieving publishing profile for {siteAndSlotName}");
            using (var cloudClient = CloudContext.Clients.CreateWebSiteManagementClient(
                new CertificateCloudCredentials(subscriptionId, new X509Certificate2(certificateBytes)),new Uri(serviceManagementEndpoint)))
            {
                var webApp = cloudClient.WebSpaces.List()
                    .SelectMany( webSpace => cloudClient.WebSpaces.ListWebSites(webSpace.Name, new WebSiteListParameters()))
                    .FirstOrDefault(webSite => webSite.Name.Equals(siteAndSlotName, StringComparison.OrdinalIgnoreCase));

                if (webApp == null)
                    throw new CommandException($"Could not find Azure WebSite '{siteAndSlotName}' in subscription '{subscriptionId}'");

                Log.Verbose("Retrieving publishing profile...");
                var publishProfile = cloudClient.WebSites.GetPublishProfile(webApp.WebSpace, siteAndSlotName)
                    .PublishProfiles.First(x => x.PublishMethod.StartsWith("MSDeploy"));

                Log.Verbose($"Retrieved publishing profile: URI: {publishProfile.PublishUrl}  UserName: {publishProfile.UserName}");
                return new SitePublishProfile(publishProfile.UserName, publishProfile.UserPassword,
                    new Uri("https://" + publishProfile.PublishUrl));
            }
        }
    }
}
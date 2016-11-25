using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Calamari.Commands.Support;
using Calamari.Extensibility;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.WebSites.Models;

namespace Calamari.Azure.Integration.Websites.Publishing
{
    public class ServiceManagementPublishProfileProvider  
    {
        public static SitePublishProfile GetPublishProperties(string subscriptionId, byte[] certificateBytes, string siteName,string serviceManagementEndpoint)
        {
            Log.Verbose($"servicemanagement endpoint is {serviceManagementEndpoint}");
            using (var cloudClient = CloudContext.Clients.CreateWebSiteManagementClient(
                new CertificateCloudCredentials(subscriptionId, new X509Certificate2(certificateBytes)),new Uri(serviceManagementEndpoint)))
            {
                var webApp = cloudClient.WebSpaces.List()
                    .SelectMany(
                        webSpace => cloudClient.WebSpaces.ListWebSites(webSpace.Name, new WebSiteListParameters {}))
                    .FirstOrDefault(webSite => webSite.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase));

                if (webApp == null)
                    throw new CommandException( $"Could not find Azure WebSite '{siteName}' in subscription '{subscriptionId}'");

                Log.Verbose("Retrieving publishing profile...");
                var publishProfile = cloudClient.WebSites.GetPublishProfile(webApp.WebSpace, siteName)
                    .PublishProfiles.First(x => x.PublishMethod.StartsWith("MSDeploy"));

                Log.Verbose($"Retrieved publishing profile: URI: {publishProfile.PublishUrl}  UserName: {publishProfile.UserName}");
                return new SitePublishProfile(publishProfile.UserName, publishProfile.UserPassword,
                    new Uri("https://" + publishProfile.PublishUrl));
            }
        }
    }
}
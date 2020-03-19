using System.Collections.Generic;
using Calamari.Azure.Accounts;
using Calamari.Azure.WebApps.Util;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Microsoft.Azure.Management.WebSites;

namespace Calamari.Azure.WebApps.Deployment.Conventions
{
    public class LogAzureWebAppDetails : IInstallConvention
    {
        readonly ILog log;

        private Dictionary<string, string> PortalLinks = new Dictionary<string, string>
        {
            { "AzureGlobalCloud", "portal.azure.com" },
            { "AzureChinaCloud", "portal.azure.cn" },
            { "AzureUSGovernment", "portal.azure.us" },
            { "AzureGermanCloud", "portal.microsoftazure.de" }
        };

        public LogAzureWebAppDetails(ILog log)
        {
            this.log = log;
        }

        public void Install(RunningDeployment deployment)
        {
            try
            {
                var variables = deployment.Variables;
                var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName, string.Empty);
                var siteAndSlotName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
                var azureEnvironment = variables.Get(SpecialVariables.Action.Azure.Environment);
                var account = new AzureServicePrincipalAccount(variables);

                var client = account.CreateWebSiteManagementClient();
                var site = client?.WebApps.Get(resourceGroupName, siteAndSlotName);
                if (site != null)
                {
                    var portalUrl = GetAzurePortalUrl(azureEnvironment);

                    log.Info($"Default Host Name: {site.DefaultHostName}");
                    log.Info($"Application state: {site.State}");
                    log.Info("Links:");
                    log.Info(log.FormatLink($"https://{site.DefaultHostName}"));

                    if (!site.HttpsOnly.HasValue || site.HttpsOnly == false)
                    {
                        log.Info(log.FormatLink($"http://{site.DefaultHostName}"));
                    }

                    string portalUri = $"https://{portalUrl}/#@/resource{site.Id}";

                    log.Info(log.FormatLink(portalUri, "View in Azure Portal"));
                }
            }
            catch
            {
                // do nothing
            }
        }

        private string GetAzurePortalUrl(string environment)
        {
            if (!string.IsNullOrEmpty(environment) && PortalLinks.ContainsKey(environment))
            {
                return PortalLinks[environment];
            }

            return PortalLinks["AzureGlobalCloud"];
        }
    }
}

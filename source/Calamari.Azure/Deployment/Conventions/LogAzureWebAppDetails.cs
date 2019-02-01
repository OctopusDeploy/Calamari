using System.Collections.Generic;
using Calamari.Azure.Accounts;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Microsoft.Azure.Management.WebSites;

namespace Calamari.Azure.Deployment.Conventions
{
    public class LogAzureWebAppDetails : IInstallConvention
    {
        private Dictionary<string, string> PortalLinks = new Dictionary<string, string>
        {
            { "AzureGlobalCloud", "portal.azure.com" },
            { "AzureChinaCloud", "portal.azure.cn" },
            { "AzureUSGovernment", "portal.azure.us" },
            { "AzureGermanCloud", "portal.microsoftazure.de" }
        };

        public void Install(RunningDeployment deployment)
        {
            try
            {
                var variables = deployment.Variables;
                var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName, string.Empty);
                var siteAndSlotName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
                var azureEnvironment = variables.Get(SpecialVariables.Action.Azure.Environment);
                var account = AccountFactory.Create(variables);

                if (account is AzureServicePrincipalAccount servicePrincipalAccount)
                {
                    var client = servicePrincipalAccount.CreateWebSiteManagementClient();
                    var site = client?.WebApps.Get(resourceGroupName, siteAndSlotName);
                    if (site != null)
                    {
                        var portalUrl = GetAzurePortalUrl(azureEnvironment);

                        Log.Info($"Default Host Name: {site.DefaultHostName}");
                        Log.Info($"Application state: {site.State}");
                        Log.Info("Links:");
                        Log.LogLink($"https://{site.DefaultHostName}");

                        if (!site.HttpsOnly.HasValue || site.HttpsOnly == false)
                        {
                            Log.LogLink($"http://{site.DefaultHostName}");
                        }

                        string portalUri = $"https://{portalUrl}/#@/resource{site.Id}";

                        Log.LogLink(portalUri, "View in Azure Portal");
                    }
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

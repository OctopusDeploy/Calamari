using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Microsoft.Azure.Management.WebSites;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AzureWebApp
{
    class LogAzureWebAppDetailsBehaviour : IDeployBehaviour
    {
        readonly ILog log;

        Dictionary<string, string> portalLinks = new Dictionary<string, string>
        {
            { "AzureGlobalCloud", "portal.azure.com" },
            { "AzureChinaCloud", "portal.azure.cn" },
            { "AzureUSGovernment", "portal.azure.us" },
            { "AzureGermanCloud", "portal.microsoftazure.de" }
        };

        public LogAzureWebAppDetailsBehaviour(ILog log)
        {
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment deployment)
        {
            try
            {
                var variables = deployment.Variables;
                var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName, string.Empty);
                var siteAndSlotName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
                var azureEnvironment = variables.Get(SpecialVariables.Action.Azure.Environment);

                WebSiteManagementClient client;
                var hasAccessToken = !variables.Get(AzureAccountVariables.AccessToken).IsNullOrEmpty();
                if (hasAccessToken)
                {
                    var account = new AzureOidcAccount(variables);
                    client = account.CreateWebSiteManagementClient();
                }
                else
                {
                    var account = new AzureServicePrincipalAccount(variables);
                    client = await account.CreateWebSiteManagementClient();
                }

                var site = await client.WebApps.GetAsync(resourceGroupName, siteAndSlotName);
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

                    var portalUri = $"https://{portalUrl}/#@/resource{site.Id}";

                    log.Info(log.FormatLink(portalUri, "View in Azure Portal"));
                }
            }
            catch
            {
                // do nothing
            }
        }

        string GetAzurePortalUrl(string environment)
        {
            if (!string.IsNullOrEmpty(environment) && portalLinks.ContainsKey(environment))
            {
                return portalLinks[environment];
            }

            return portalLinks["AzureGlobalCloud"];
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.Azure.AppServices;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureWebApp
{
    class LogAzureWebAppDetailsBehaviour : IDeployBehaviour
    {
        readonly ILog log;

        readonly Dictionary<string, string> portalLinks = new Dictionary<string, string>
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

                var account = AzureAccountFactory.Create(variables);
                var armClient = account.CreateArmClient();

                var targetSite = AzureTargetSite.Create(account, variables, log);

                var resourceGroupResource = armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(targetSite.SubscriptionId, targetSite.ResourceGroupName));
                if (!await resourceGroupResource.GetWebSites().ExistsAsync(targetSite.Site))
                {
                    Log.Error($"Azure Web App '{targetSite.Site}' could not be found in resource group '{resourceGroupName}'. Either it does not exist, or the Azure Account in use may not have permissions to access it.");
                    throw new Exception("Web App not found");
                }

                var webSiteData = await armClient.GetWebSiteDataAsync(targetSite);

                var portalUrl = GetAzurePortalUrl(azureEnvironment);

                log.Info($"Default Host Name: {webSiteData.DefaultHostName}");
                log.Info($"Application state: {webSiteData.State}");
                log.Info("Links:");
                log.Info(log.FormatLink($"https://{webSiteData.DefaultHostName}"));

                if (!webSiteData.IsHttpsOnly.GetValueOrDefault())
                {
                    log.Info(log.FormatLink($"http://{webSiteData.DefaultHostName}"));
                }

                var portalUri = $"https://{portalUrl}/#@/resource{webSiteData.Id}";

                log.Info(log.FormatLink(portalUri, "View in Azure Portal"));
            }
            catch
            {
                // do nothing
            }
        }

        string GetAzurePortalUrl(string environment)
        {
            if (!string.IsNullOrEmpty(environment) && portalLinks.TryGetValue(environment, out var url))
            {
                return url;
            }

            return portalLinks["AzureGlobalCloud"];
        }
    }
}
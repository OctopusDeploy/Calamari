using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Calamari.Azure.Accounts;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.Certificates;
using Calamari.Integration.Processes;
using Microsoft.Azure.Management.WebSites;
using Microsoft.WindowsAzure.Management.WebSites;
using Microsoft.WindowsAzure.Management.WebSites.Models;

namespace Calamari.Azure.Commands
{
    [Command("hc-azure-web", Description = "Run a health check on an Azure Web Application")]
    public class HealthCheckAzureWebAppCommand : Command
    {
        private readonly ILog log;
        private readonly ICertificateStore certificateStore;
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;

        public HealthCheckAzureWebAppCommand(ILog log, ICertificateStore certificateStore)
        {
            this.log = log;
            this.certificateStore = certificateStore;
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);

            var account = AccountFactory.Create(variables);

            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);

            if (account is AzureAccount azureAccount)
            {
                ConfirmWebAppExists(azureAccount, resourceGroupName, webAppName);
            }
            else if (account is AzureServicePrincipalAccount servicePrincipalAccount)
            {
                ConfirmWebAppExists(servicePrincipalAccount, resourceGroupName, webAppName);
            }

            return 0;
        }

        void ConfirmWebAppExists(AzureAccount certificateAccount, string resourceGroupName, string siteName)
        {
            log.Warn("Azure have announced they will be retiring Service Management API support on June 30th 2018. Please switch to using Service Principals for your Octopus Azure accounts https://g.octopushq.com/AzureServicePrincipalAccount");
            using (var azureClient = certificateAccount.CreateWebSiteManagementClient(certificateStore))
            {
                var response = azureClient.WebSpaces.List();
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new Exception("Azure returned HTTP status-code getting WebSpaces: " + response.StatusCode);

                // Confirm the site exists.
                var matchingSite = response.WebSpaces
                    .SelectMany(webSpace =>
                    {
                        var webAppsResponse = azureClient.WebSpaces.ListWebSites(
                            webSpace.Name, new WebSiteListParameters
                            {
                                PropertiesToInclude = new List<string> { "Name", "WebSpace" }
                            });
                        if (webAppsResponse.StatusCode != HttpStatusCode.OK)
                            throw new Exception($"Azure returned HTTP status-code getting WebApps for the WebSpace '{webSpace}': {webAppsResponse.StatusCode}");
                        return webAppsResponse.WebSites.Where(
                            x => string.Equals(x.Name, siteName, StringComparison.CurrentCultureIgnoreCase)
                            && x.WebSpace.ToLower().Contains(resourceGroupName.ToLower())
                        );
                    }).FirstOrDefault();
                if (matchingSite == null)
                    throw new Exception($"Could not find site {siteName} in resource group {resourceGroupName}, using Management Certificate with subscription {certificateAccount.SubscriptionNumber}");
            }
        }

        void ConfirmWebAppExists(AzureServicePrincipalAccount servicePrincipalAccount, string resourceGroupName, string siteAndSlotName)
        {
            using (var webSiteClient = servicePrincipalAccount.CreateWebSiteManagementClient())
            {
                var matchingSite = webSiteClient.WebApps
                    .ListByResourceGroup(resourceGroupName, true)
                    .ToList()
                    .FirstOrDefault(x => string.Equals(x.Name, siteAndSlotName, StringComparison.CurrentCultureIgnoreCase));
                if (matchingSite == null)
                    throw new Exception($"Could not find site {siteAndSlotName} in resource group {resourceGroupName}, using Service Principal with subscription {servicePrincipalAccount.SubscriptionNumber}");
            }
        }

    }
}
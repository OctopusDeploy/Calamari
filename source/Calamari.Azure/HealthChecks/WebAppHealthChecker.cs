﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Calamari.Azure.Accounts;
using Calamari.Deployment;
using Calamari.HealthChecks;
using Calamari.Integration.Certificates;
using Calamari.Integration.Processes;
using Microsoft.Azure.Management.WebSites;
using Microsoft.WindowsAzure.Management.WebSites;
using Microsoft.WindowsAzure.Management.WebSites.Models;

namespace Calamari.Azure.HealthChecks
{
    public class WebAppHealthChecker : IDoesDeploymentTargetTypeHealthChecks
    {
        private readonly ILog log;
        private readonly ICertificateStore certificateStore;

        public WebAppHealthChecker(ILog log, ICertificateStore certificateStore)
        {
            this.log = log;
            this.certificateStore = certificateStore;
        }

        public bool HandlesDeploymentTargetTypeName(string deploymentTargetTypeName)
        {
            return deploymentTargetTypeName == "AzureWebApp";
        }

        public int ExecuteHealthCheck(CalamariVariableDictionary variables)
        {
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
            log.Warn($"Azure have announced they will be retiring Service Management API support on June 30th 2018. Please switch to using Service Principals for your Octopus Azure accounts {Log.Link("https://g.octopushq.com/AzureServicePrincipalAccount")}");
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
                            x => string.Equals(x.Name, siteName, StringComparison.OrdinalIgnoreCase)
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
                    .FirstOrDefault(x => string.Equals(x.Name, siteAndSlotName, StringComparison.OrdinalIgnoreCase));
                if (matchingSite == null)
                    throw new Exception($"Could not find site {siteAndSlotName} in resource group {resourceGroupName}, using Service Principal with subscription {servicePrincipalAccount.SubscriptionNumber}");
            }
        }
    }
}
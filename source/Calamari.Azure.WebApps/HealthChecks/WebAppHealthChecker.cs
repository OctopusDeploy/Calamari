using System;
using System.Linq;
using Calamari.Azure.Accounts;
using Calamari.Azure.WebApps.Util;
using Calamari.Deployment;
using Calamari.HealthChecks;
using Calamari.Integration.Certificates;
using Calamari.Integration.Processes;
using Microsoft.Azure.Management.WebSites;

namespace Calamari.Azure.WebApps.HealthChecks
{
    public class WebAppHealthChecker : IDoesDeploymentTargetTypeHealthChecks
    {
        readonly IVariables variables;

        public WebAppHealthChecker(IVariables variables)
        {
            this.variables = variables;
        }

        public bool HandlesDeploymentTargetTypeName(string deploymentTargetTypeName)
        {
            return deploymentTargetTypeName == "AzureWebApp";
        }

        public int ExecuteHealthCheck()
        {
            var account = new AzureServicePrincipalAccount(variables);

            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);

            ConfirmWebAppExists(account, resourceGroupName, webAppName);

            return 0;
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
using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.WebSites;

namespace Calamari.AzureWebApp
{
    [Command("health-check", Description = "Run a health check on a DeploymentTargetType")]
    public class HealthCheckCommand : ICommand
    {
        readonly IVariables variables;

        public HealthCheckCommand(IVariables variables)
        {
            this.variables = variables;
        }

        public int Execute()
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
                var matchingSite = webSiteClient.WebApps.Get(resourceGroupName, siteAndSlotName);
                if (matchingSite == null)
                {
                    throw new Exception($"Could not find site {siteAndSlotName} in resource group {resourceGroupName}, using Service Principal with subscription {servicePrincipalAccount.SubscriptionNumber}");
                }
            }
        }
    }
}
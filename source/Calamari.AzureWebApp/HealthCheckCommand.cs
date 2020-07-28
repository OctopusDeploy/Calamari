using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;
using Microsoft.Azure.Management.WebSites;

namespace Calamari.AzureWebApp
{
    [Command("health-check", Description = "Run a health check on a DeploymentTargetType")]
    public class HealthCheckCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<HealthCheckBehaviour>();
        }
    }

    class HealthCheckBehaviour: IDeployBehaviour
    {
        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            var account = new AzureServicePrincipalAccount(context.Variables);

            var resourceGroupName = context.Variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            var webAppName = context.Variables.Get(SpecialVariables.Action.Azure.WebAppName);

            return ConfirmWebAppExists(account, resourceGroupName, webAppName);
        }

        async Task ConfirmWebAppExists(AzureServicePrincipalAccount servicePrincipalAccount, string resourceGroupName, string siteAndSlotName)
        {
            using (var webSiteClient = await servicePrincipalAccount.CreateWebSiteManagementClient())
            {
                var matchingSite = await webSiteClient.WebApps.GetAsync(resourceGroupName, siteAndSlotName);
                if (matchingSite == null)
                {
                    throw new Exception($"Could not find site {siteAndSlotName} in resource group {resourceGroupName}, using Service Principal with subscription {servicePrincipalAccount.SubscriptionNumber}");
                }
            }
        }
    }
}
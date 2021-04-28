using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Calamari.AzureAppService
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
            var account = new ServicePrincipalAccount(context.Variables);

            var resourceGroupName = context.Variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            var webAppName = context.Variables.Get(SpecialVariables.Action.Azure.WebAppName);

            return ConfirmWebAppExists(account, resourceGroupName, webAppName);
        }

        async Task ConfirmWebAppExists(ServicePrincipalAccount servicePrincipal, string resourceGroupName, string siteAndSlotName)
        {
            var azureClient = servicePrincipal.CreateAzureClient(); 
            var webApp = await azureClient.WebApps.GetByResourceGroupAsync(resourceGroupName, siteAndSlotName);
            if (webApp == null)
            {
                throw new Exception($"Could not find site {siteAndSlotName} in resource group {resourceGroupName}, using Service Principal with subscription {servicePrincipal.SubscriptionNumber}");
            }
        }
    }
}
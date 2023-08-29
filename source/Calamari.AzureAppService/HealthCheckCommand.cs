using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

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

    class HealthCheckBehaviour : IDeployBehaviour
    {
        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            var account = ServicePrincipalAccount.CreateFromKnownVariables(context.Variables);

            var resourceGroupName = context.Variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            var webAppName = context.Variables.Get(SpecialVariables.Action.Azure.WebAppName);

            return ConfirmWebAppExists(account, resourceGroupName, webAppName);
        }

        private async Task ConfirmWebAppExists(ServicePrincipalAccount servicePrincipal, string resourceGroupName, string siteAndSlotName)
        {
            var client = servicePrincipal.CreateArmClient();

            var resourceGroupResource = client.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(servicePrincipal.SubscriptionNumber, resourceGroupName));

            //if the website doesn't exist, throw
            if (!await resourceGroupResource.GetWebSites().ExistsAsync(siteAndSlotName))
                throw new Exception($"Could not find site {siteAndSlotName} in resource group {resourceGroupName}, using Service Principal with subscription {servicePrincipal.SubscriptionNumber}");
        }
    }
}
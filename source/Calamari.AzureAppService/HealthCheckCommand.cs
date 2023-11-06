using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;
using Octopus.CoreUtilities.Extensions;
using AccountVariables = Calamari.AzureAppService.Azure.AccountVariables;

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
            var hasJwt = !context.Variables.Get(AccountVariables.Jwt).IsNullOrEmpty();
            var account = hasJwt ? (IAzureAccount)new AzureOidcAccount(context.Variables) : new AzureServicePrincipalAccount(context.Variables);

            var resourceGroupName = context.Variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            var webAppName = context.Variables.Get(SpecialVariables.Action.Azure.WebAppName);

            return ConfirmWebAppExists(account, resourceGroupName, webAppName);
        }

        private async Task ConfirmWebAppExists(IAzureAccount azureAccount, string resourceGroupName, string siteAndSlotName)
        {
            var client = azureAccount.CreateArmClient();

            var resourceGroupResource = client.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(azureAccount.SubscriptionNumber, resourceGroupName));

            //if the website doesn't exist, throw
            if (!await resourceGroupResource.GetWebSites().ExistsAsync(siteAndSlotName))
                throw new Exception($"Could not find site {siteAndSlotName} in resource group {resourceGroupName}, using Service Principal with subscription {azureAccount.SubscriptionNumber}");
        }
    }
}
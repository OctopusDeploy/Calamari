using System;
using Sashimi.AzureWebApp.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.CommandBuilders;

namespace Sashimi.AzureWebApp
{
    class AzureWebAppPackageContributor : IContributeToPackageDeployment
    {
        public bool CanContribute(DeploymentTargetType deploymentTargetType)
        {
            return deploymentTargetType == AzureWebAppEndpoint.AzureWebAppDeploymentTargetType;
        }

        public bool IsValid(IActionHandlerContext context, out IActionHandlerResult result)
        {
            result = ActionHandlerResult.FromSuccess();
            return true;
        }

        public ICalamariCommandBuilder Create(IActionHandlerContext context)
        {
            return context
                .CalamariCommand(AzureConstants.CalamariAzure, "deploy-azure-web");
        }
    }
}
using System;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureWebApp
{
    class AzureWebAppPackageContributor : IContributeToPackageDeployment
    {
        public PackageContributionResult Contribute(DeploymentTargetType deploymentTargetType, IActionHandlerContext context)
        {
            return deploymentTargetType == AzureWebAppEndpoint.AzureWebAppDeploymentTargetType
                ? PackageContributionResult.RedirectToHandler<AzureWebAppActionHandler>()
                : PackageContributionResult.DoDefaultPackageDeployment();
        }
    }
}
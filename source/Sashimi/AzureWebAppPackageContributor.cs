using System;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.AzureWebApp.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureWebApp
{
    class AzureWebAppPackageContributor : IContributeToPackageDeployment
    {
        public PackageContributionResult Contribute(DeploymentTargetType deploymentTargetType, IActionHandlerContext context, ITaskLog taskLog)
        {
            return deploymentTargetType == AzureWebAppEndpoint.AzureWebAppDeploymentTargetType
                ? PackageContributionResult.RedirectToHandler<AzureWebAppActionHandler>()
                : PackageContributionResult.DoDefaultPackageDeployment();
        }
    }
}
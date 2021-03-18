using System;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.AzureCloudService.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureCloudService
{
    class AzureCloudServicePackageContributor : IContributeToPackageDeployment
    {
        public PackageContributionResult Contribute(DeploymentTargetType deploymentTargetType, IActionHandlerContext context, ITaskLog taskLog)
        {
            return deploymentTargetType == AzureCloudServiceEndpoint.AzureCloudServiceDeploymentTargetType
                ? PackageContributionResult.RedirectToHandler<AzureCloudServiceActionHandler>()
                : PackageContributionResult.DoDefaultPackageDeployment();
        }
    }
}
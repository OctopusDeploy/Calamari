using System;
using Sashimi.AzureCloudService.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureCloudService
{
    class AzureCloudServicePackageContributor : IContributeToPackageDeployment
    {
        public PackageContributionResult Contribute(DeploymentTargetType deploymentTargetType, IActionHandlerContext context)
        {
            return deploymentTargetType == AzureCloudServiceEndpoint.AzureCloudServiceDeploymentTargetType
                ? PackageContributionResult.RedirectToHandler<AzureCloudServiceActionHandler>()
                : PackageContributionResult.DoDefaultPackageDeployment();
        }
    }
}
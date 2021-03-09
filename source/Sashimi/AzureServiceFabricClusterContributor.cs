using System;
using Octopus.CoreUtilities;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.AzureServiceFabric.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureServiceFabric
{
    class AzureServiceFabricClusterContributor : IContributeToPackageDeployment
    {
        public PackageContributionResult Contribute(DeploymentTargetType deploymentTargetType, IActionHandlerContext context, ITaskLog taskLog)
        {
            if (deploymentTargetType == AzureServiceFabricClusterEndpoint.AzureServiceFabricClusterDeploymentTargetType)
            {
                taskLog.Info($"The machine {context.DeploymentTargetName.SomeOr("<unknown>")} will not be deployed to because it is a Service Fabric Cluster.");
                return PackageContributionResult.SkipPackageDeployment();
            }

            return PackageContributionResult.DoDefaultPackageDeployment();
        }
    }
}
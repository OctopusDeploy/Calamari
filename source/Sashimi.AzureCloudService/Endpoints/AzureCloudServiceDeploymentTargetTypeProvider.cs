using System;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureCloudService.Endpoints
{
    class AzureCloudServiceDeploymentTargetTypeProvider : IDeploymentTargetTypeProvider
    {
        public DeploymentTargetType DeploymentTargetType => AzureCloudServiceEndpoint.AzureCloudServiceDeploymentTargetType;
        public Type DomainType => typeof(AzureCloudServiceEndpoint);

        public Type ApiType => typeof(AzureCloudServiceResource);
    }
}
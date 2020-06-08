using System;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureServiceFabric.Endpoints
{
    class AzureServiceFabricClusterDeploymentTargetTypeProvider : IDeploymentTargetTypeProvider
    {
        public DeploymentTargetType DeploymentTargetType => AzureServiceFabricClusterEndpoint.AzureServiceFabricClusterDeploymentTargetType;
        public Type DomainType => typeof(AzureServiceFabricClusterEndpoint);

        public Type ApiType => typeof(ServiceFabricEndpointResource);
    }
}
using System;
using System.Collections.Generic;
using FluentValidation;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureServiceFabric.Endpoints
{
    class AzureServiceFabricClusterDeploymentTargetTypeProvider : IDeploymentTargetTypeProvider
    {
        public DeploymentTargetType DeploymentTargetType => AzureServiceFabricClusterEndpoint.AzureServiceFabricClusterDeploymentTargetType;
        public Type DomainType => typeof(AzureServiceFabricClusterEndpoint);
        public Type ApiType => typeof(ServiceFabricEndpointResource);
        public IValidator Validator => new AzureServiceFabricClusterEndpointValidator();

        public IEnumerable<AccountType> SupportedAccountTypes
        {
            get { yield break; }
        }
    }
}
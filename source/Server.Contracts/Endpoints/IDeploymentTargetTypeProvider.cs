using System;

namespace Sashimi.Server.Contracts.Endpoints
{
    public interface IDeploymentTargetTypeProvider
    {
        DeploymentTargetType DeploymentTargetType { get; }
        Type DomainType { get; }
        Type ApiType { get; }
    }
}
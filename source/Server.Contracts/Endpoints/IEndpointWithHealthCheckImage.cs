using System;

namespace Sashimi.Server.Contracts.Endpoints
{
    public interface IEndpointWithHealthCheckImage
    {
        DeploymentActionContainer Container { get; set; }
    }
}
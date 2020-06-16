using System;

namespace Sashimi.Server.Contracts.Endpoints
{
    public interface IEndpointWithProxy
    {
        string ProxyId { get; set; }
    }
}
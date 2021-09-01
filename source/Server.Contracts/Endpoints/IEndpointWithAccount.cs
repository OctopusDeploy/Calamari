using System;

namespace Sashimi.Server.Contracts.Endpoints
{
    public interface IEndpointWithAccount
    {
        string AccountId { get; }
    }
}
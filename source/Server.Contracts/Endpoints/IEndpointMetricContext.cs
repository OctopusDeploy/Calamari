using System;
using System.Collections.Generic;

namespace Sashimi.Server.Contracts.Endpoints
{
    public interface IEndpointMetricContext
    {
        IEnumerable<T> GetEndpoints<T>() where T : Endpoint;
    }
}
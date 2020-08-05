using System.Collections.Generic;

namespace Sashimi.Server.Contracts.Endpoints
{
    public interface IEndpointWithServerCertificates
    {
        IEnumerable<string> ServerCertificateIds { get; }
    }
}
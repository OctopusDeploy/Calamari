using System.Collections.Generic;

namespace Sashimi.Server.Contracts.Endpoints
{
    public interface IEndpointWithClientCertificates
    {
        IEnumerable<string> ClientCertificateIds { get; }
    }
}
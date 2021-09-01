using System;
using System.Collections.Generic;

namespace Sashimi.Server.Contracts.Endpoints
{
    public interface IEndpointWithServerCertificates
    {
        // todo https://github.com/OctopusDeploy/Sashimi/pull/76#discussion_r465399149
        IEnumerable<string> ServerCertificateIds { get; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources;

public class EndpointSlice : Resource
{
    public IEnumerable<string> Endpoints { get; }
    public override ResourceStatus Status { get; }

    public EndpointSlice(JObject json) : base(json)
    {
        Endpoints = Data.SelectTokens("$.endpoints[*].addresses[0]").Values<string>();
        
        Status = ResourceStatus.Successful;
    }

    public override bool HasUpdate(Resource lastStatus)
    {
        var last = CastOrThrow<EndpointSlice>(lastStatus);
        return !last.Endpoints.SequenceEqual(Endpoints);
    }
}
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class EndpointSlice : Resource
    {
        public IEnumerable<string> Endpoints { get; }

        public EndpointSlice(JObject json, string cluster) : base(json, cluster)
        {
            Endpoints = data.SelectTokens("$.endpoints[*].addresses[0]").Values<string>();
        }
    
        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<EndpointSlice>(lastStatus);
            return !last.Endpoints.SequenceEqual(Endpoints);
        }
    }
}
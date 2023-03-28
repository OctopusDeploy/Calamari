using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class EndpointSlice : Resource
    {
        public string AddressType { get; }
        public IEnumerable<string> Ports { get; }
        public IEnumerable<string> Endpoints { get; }

        public EndpointSlice(JObject json) : base(json)
        {
            AddressType = Field("$.addressType");
            
            var ports = data.SelectToken("$.ports")
                ?.ToObject<PortEntry[]>() ?? new PortEntry[] { };
            Ports = ports.Select(port => port.Port.ToString());

            var endpoints = data.SelectToken("$.endpoints")
                ?.ToObject<EndpointEntry[]>() ?? new EndpointEntry[] { };
            Endpoints = FormatEndpoints(endpoints);
        }
    
        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<EndpointSlice>(lastStatus);
            return last.AddressType != AddressType || !last.Ports.SequenceEqual(Ports) || !last.Endpoints.SequenceEqual(Endpoints);
        }

        private static IEnumerable<string> FormatEndpoints(IEnumerable<EndpointEntry> endpoints)
        {
            return endpoints.SelectMany(endpoint => endpoint.Addresses);
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Service : Resource
    {
        public override string ChildKind => "EndpointSlice";

        public string Type { get; }
        public string ClusterIp { get; }
        public IEnumerable<string> Ports { get; }

        public Service(JObject json) : base(json)
        {
            Type = Field("$.spec.type");
            ClusterIp = Field("$.spec.clusterIP");

            var ports = data.SelectToken("$.spec.ports")
                ?.ToObject<PortEntry[]>() ?? new PortEntry[] { };
            Ports = FormatPorts(ports);
        }
    
        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Service>(lastStatus);
            return last.ClusterIp != ClusterIp || last.Type != Type || last.Ports.SequenceEqual(Ports);
        }

        private static IEnumerable<string> FormatPorts(IEnumerable<PortEntry> ports)
        {
            return ports.Select(port => port.NodePort == null
                ? $"{port.Port}/{port.Protocol}"
                : $"{port.Port}:{port.NodePort}/{port.Protocol}");
        }
    }
}
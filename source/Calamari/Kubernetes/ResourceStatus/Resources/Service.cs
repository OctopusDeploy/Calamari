using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Service : Resource
    {
        public override string ChildKind => "EndpointSlice";

        public string Type { get; }
        public string ClusterIp { get; }
        public string Ports { get; }

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
            return last.ClusterIp != ClusterIp || last.Type != Type || last.Ports != Ports;
        }

        private static string FormatPorts(IEnumerable<PortEntry> ports)
        {
            return string.Join(',', ports.Select(port => string.IsNullOrEmpty(port.NodePort)
                ? $"{port.Port}/{port.Protocol}"
                : $"{port.Port}:{port.NodePort}/{port.Protocol}"));
        }
    }

    public class PortEntry
    {
        [JsonProperty("port")]
        public string Port { get; set; }
        
        [JsonProperty("nodePort", Required = Required.AllowNull)]
        public string NodePort { get; set; }
        
        [JsonProperty("protocol")]
        public string Protocol { get; set; }
    }
}
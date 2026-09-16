using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Service : Resource
    {
        public override ResourceGroupVersionKind ChildGroupVersionKind => SupportedResourceGroupVersionKinds.EndpointSliceV1;

        public string Type { get; }
        public string ClusterIp { get; }
        public string ExternalIp { get; }
        public IEnumerable<string> Ports { get; }

        public Service(JObject json, Options options) : base(json, options)
        {
            Type = Field(json, "$.spec.type");
            ClusterIp = Field(json, "$.spec.clusterIP");

            var ports = json.SelectToken("$.spec.ports")
                ?.ToObject<ServicePort[]>() ?? new ServicePort[] { };
            Ports = FormatPorts(ports);

            var loadBalancerIngresses = json.SelectToken("$.status.loadBalancer.ingress")
                ?.ToObject<LoadBalancerIngress[]>() ?? new LoadBalancerIngress[] { };

            ExternalIp = FormatExternalIp(loadBalancerIngresses);
        }
    
        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Service>(lastStatus);
            return last.ClusterIp != ClusterIp || last.ExternalIp != ExternalIp || last.Type != Type || last.Ports.SequenceEqual(Ports);
        }

        private static IEnumerable<string> FormatPorts(IEnumerable<ServicePort> ports)
        {
            return ports.Select(port => port.NodePort == null
                ? $"{port.Port}/{port.Protocol}"
                : $"{port.Port}:{port.NodePort}/{port.Protocol}");
        }

        private static string FormatExternalIp(LoadBalancerIngress[] loadBalancerIngresses)
        {
            return !loadBalancerIngresses.Any() ? "<none>" : string.Join(",", loadBalancerIngresses.Select(ingress => ingress.Ip));
        }
    }
}
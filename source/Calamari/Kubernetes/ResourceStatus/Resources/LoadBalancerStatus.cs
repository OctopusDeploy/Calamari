using System.Collections.Generic;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    // Subset of: https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.26/#loadbalancerstatus-v1-core
    public class LoadBalancerStatus
    {
        [JsonProperty("ingress")]
        public IEnumerable<LoadBalancerIngress> Ingress { get; set; }
    }

    public class LoadBalancerIngress
    {
        [JsonProperty("hostname")]
        public string Hostname { get; set; }
        
        [JsonProperty("ip")]
        public string Ip { get; set; }
        
        [JsonProperty("ports")]
        public IEnumerable<PortStatus> Ports { get; set; }
    }
}


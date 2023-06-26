using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    // Subset of: https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.26/#portstatus-v1-core
    public class PortStatus
    {
        [JsonProperty("port")]
        public string Port { get; set; }
        
        [JsonProperty("protocol")]
        public string Protocol { get; set; }
    }
}


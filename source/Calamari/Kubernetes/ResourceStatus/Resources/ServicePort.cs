using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    // Subset of: https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.26/#serviceport-v1-core
    public class ServicePort
    {
        [JsonProperty("port")]
        public int Port { get; set; }
        
        [JsonProperty("nodePort")]
        public int? NodePort { get; set; }
        
        [JsonProperty("protocol")]
        public string Protocol { get; set; }
    }
}
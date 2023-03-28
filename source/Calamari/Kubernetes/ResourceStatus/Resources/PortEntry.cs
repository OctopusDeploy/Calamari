using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class PortEntry
    {
        [JsonProperty("port")]
        public int Port { get; set; }
        
        [JsonProperty("nodePort")]
        public int? NodePort { get; set; }
        
        [JsonProperty("protocol")]
        public string Protocol { get; set; }
    }
}
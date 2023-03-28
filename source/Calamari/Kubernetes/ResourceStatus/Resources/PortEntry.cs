using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
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
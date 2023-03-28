using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class EndpointEntry
    {
        [JsonProperty("addresses")]
        public string[] Addresses { get; set; }
    }
}


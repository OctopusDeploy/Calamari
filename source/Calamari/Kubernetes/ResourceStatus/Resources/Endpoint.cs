using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    // Subset of: https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.22/#endpoint-v1-discovery-k8s-io
    public class Endpoint
    {
        [JsonProperty("addresses")]
        public string[] Addresses { get; set; }
    }
}


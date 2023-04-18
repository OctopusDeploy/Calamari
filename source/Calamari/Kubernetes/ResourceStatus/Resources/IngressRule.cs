using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    // subset of: https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.26/#ingressrule-v1-networking-k8s-io
    public class IngressRule
    {
        [JsonProperty("host")]
        public string Host { get; set; }
    }
}


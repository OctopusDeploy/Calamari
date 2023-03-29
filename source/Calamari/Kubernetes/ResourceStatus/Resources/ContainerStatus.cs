using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    // Subset of: https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.22/#containerstatus-v1-core
    public class ContainerStatus
    {
        [JsonProperty("state")]
        public ContainerState State { get; set; }
        
        [JsonProperty("ready")]
        public bool Ready { get; set; }
        
        [JsonProperty("restartCount")]
        public int RestartCount { get; set; }
    }

    // Subset of: https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.22/#containerstate-v1-core
    public class ContainerState
    {
        [JsonProperty("running")]
        public ContainerStateRunning Running { get; set; }
        
        [JsonProperty("waiting")]
        public ContainerStateWaiting Waiting { get; set; }
        
        [JsonProperty("terminated")]
        public ContainerStateTerminated Terminated { get; set; }
    }
    
    public class ContainerStateRunning {}
    
    public class ContainerStateWaiting
    {
        [JsonProperty("reason")]
        public string Reason { get; set; }
    }
    
    public class ContainerStateTerminated
    {
        [JsonProperty("reason")]
        public string Reason { get; set; }
    }
}
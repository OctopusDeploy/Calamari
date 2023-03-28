using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class ContainerStatus
    {
        [JsonProperty("state")]
        public State State { get; set; }
    }

    public class State
    {
        [JsonProperty("running")]
        public Running Running { get; set; }
        
        [JsonProperty("waiting")]
        public Waiting Waiting { get; set; }
        
        [JsonProperty("terminated")]
        public Terminated Terminated { get; set; }
    }
    
    public class Running {}
    
    public class Waiting
    {
        [JsonProperty("reason")]
        public string Reason { get; set; }
    }
    
    public class Terminated
    {
        [JsonProperty("reason")]
        public string Reason { get; set; }
    }
}
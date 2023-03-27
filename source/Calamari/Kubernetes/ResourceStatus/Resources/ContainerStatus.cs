using System.Text.Json.Serialization;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class ContainerStatus
    {
        [JsonPropertyName("state")]
        public State State { get; set; }
    }

    public class State
    {
        [JsonPropertyName("running")]
        public Running Running { get; set; }
        
        [JsonPropertyName("waiting")]
        public Waiting Waiting { get; set; }
        
        [JsonPropertyName("terminated")]
        public Terminated Terminated { get; set; }
    }
    
    public class Running {}
    
    public class Waiting
    {
        [JsonPropertyName("reason")]
        public string Reason { get; set; }
    }
    
    public class Terminated
    {
        [JsonPropertyName("reason")]
        public string Reason { get; set; }
    }
}
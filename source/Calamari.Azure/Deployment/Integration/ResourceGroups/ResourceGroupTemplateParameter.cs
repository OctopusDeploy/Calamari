using Newtonsoft.Json;

namespace Calamari.Azure.Deployment.Integration
{
    public class ResourceGroupTemplateParameter
    {
        [JsonProperty("value")]
        public object Value { get; set; }
    }
}
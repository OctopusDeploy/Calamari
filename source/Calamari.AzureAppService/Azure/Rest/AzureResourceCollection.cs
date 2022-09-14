using Newtonsoft.Json;

namespace Calamari.AzureAppService.Azure.Rest
{
    public class AzureResourceCollection
    {
        [JsonProperty("value")]
        public AzureResource[] Resources { get; set; }
    }
}
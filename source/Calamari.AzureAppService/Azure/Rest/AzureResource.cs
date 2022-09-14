using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Calamari.AzureAppService.Azure.Rest
{
    public class AzureResource
    {
        [JsonProperty("id")]
        public string Id {get; set;}

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("tags")]
        public Dictionary<string,string> Tags { get; set; }

        [JsonProperty("properties")]
        public AzureResourceProperties Properties { get; set; }
    }

    public class AzureResourceProperties
    {
        [JsonProperty("resourceGroup")]
        public string ResourceGroup { get; set; }
    }
}
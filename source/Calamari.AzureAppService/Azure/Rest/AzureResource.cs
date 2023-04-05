using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Calamari.AzureAppService.Azure.Rest
{
    public class AzureResource
    {
        // [JsonProperty("id")]
        // public string Id {get; set;}

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("resourceGroup")]
        public string ResourceGroup { get; set; }

        // [JsonProperty("kind")]
        // public string Kind { get; set; }
        //
        // [JsonProperty("location")]
        // public string Location { get; set; }

        [JsonProperty("tags")]
        public Dictionary<string,string> Tags { get; set; }

        // [JsonProperty("properties")]
        // public AzureResourceProperties Properties { get; set; }

        [JsonIgnore]
        public bool IsSlot => Type.EndsWith("/slots");

        [JsonIgnore]
        public string SlotName
        {
            get
            {
                if (!IsSlot)
                {
                    return null;
                }
                var indexOfSlash = Name.LastIndexOf("/", StringComparison.InvariantCulture);
                return indexOfSlash < 0 ? null : Name.Substring(indexOfSlash + 1);
            }
        }

        [JsonIgnore]
        public string ParentName
        {
            get
            {
                if (!IsSlot)
                {
                    return null;
                }
                var indexOfSlash = Name.LastIndexOf("/", StringComparison.InvariantCulture);
                return indexOfSlash < 0 ? null : Name.Substring(0, indexOfSlash + 1);
            }
        }
    }

    // public class AzureResourceProperties
    // {
    //     [JsonProperty("resourceGroup")]
    //     public string ResourceGroup { get; set; }
    // }
}
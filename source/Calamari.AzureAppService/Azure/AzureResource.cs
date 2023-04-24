using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Calamari.AzureAppService.Azure
{
    public class AzureResource
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("resourceGroup")]
        public string ResourceGroup { get; set; }

        [JsonProperty("tags")]
        public Dictionary<string,string> Tags { get; set; }

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
                return indexOfSlash < 0 ? "" : Name.Substring(indexOfSlash + 1);
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
                return indexOfSlash < 0 ? Name : Name.Substring(0, indexOfSlash);
            }
        }
    }
}
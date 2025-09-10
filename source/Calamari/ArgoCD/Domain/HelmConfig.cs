using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain
{
    public class HelmConfig
    {
        [JsonPropertyName("valueFiles")]
        public List<string> ValueFiles { get; set; } = new List<string>();
    }
}

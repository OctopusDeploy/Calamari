using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain
{
    public class ApplicationSpec
    {
        [JsonPropertyName("destination")]
        public Destination Destination { get; set; } = new Destination();

        [JsonPropertyName("project")]
        public string Project { get; set; } = string.Empty;

        // Always a list - handles both single source and multiple sources
        public List<SourceBase> Sources { get; set; } = new List<SourceBase>();
    }
}

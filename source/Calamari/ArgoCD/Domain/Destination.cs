using System;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain
{
    public class Destination
    {
        [JsonPropertyName("server")]
        public string Server { get; set; } = string.Empty;

        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = string.Empty;
    }
}

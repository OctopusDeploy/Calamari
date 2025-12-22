using System;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain
{
    public class ApplicationSource
    {
        [JsonPropertyName("repoURL")]
        public Uri RepoUrl { get; set; } = new Uri("about:blank");
    
        [JsonPropertyName("targetRevision")]
        public string TargetRevision { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string? Path { get; set; }
        
        [JsonPropertyName("helm")]
        public HelmConfig? Helm { get; set; }
        
        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        public SourceType? SourceType { get; set; }
    }
}

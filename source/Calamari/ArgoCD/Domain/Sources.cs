using System;
using System.Text.Json.Serialization;
using Calamari.ArgoCD.Git;

namespace Calamari.ArgoCD.Domain
{
    public class ApplicationSource
    {
        [JsonPropertyName("repoURL")]
        public string RepoUrl { get; set; } = string.Empty;

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

        [JsonIgnore]
        public GitRepositoryAddress Address => new(RepoUrl);
    }
}

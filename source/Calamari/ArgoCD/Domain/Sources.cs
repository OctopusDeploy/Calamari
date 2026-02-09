using System;
using System.Text.Json.Serialization;
using Calamari.ArgoCD.Git;

namespace Calamari.ArgoCD.Domain
{
    public class ApplicationSource
    {
        string originalRepoUrl = string.Empty;
        [JsonPropertyName("repoURL")]
        public string OriginalRepoUrl {
            get => originalRepoUrl;
            set
            {
                originalRepoUrl = value;
                CloneSafeRepoUrl = GitCloneSafeUrl.FromString(value);
            }
        }

        public Uri CloneSafeRepoUrl { get; private set; }
    
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
    }
}

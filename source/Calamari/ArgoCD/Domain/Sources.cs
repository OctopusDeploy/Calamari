using System;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain
{
    // Base class for polymorphic source handling
    [JsonDerivedType(typeof(BasicSource), "basic")]
    [JsonDerivedType(typeof(HelmSource), "helm")]
    [JsonDerivedType(typeof(ReferenceSource), "reference")]
    public class SourceBase
    {
        [JsonPropertyName("repoURL")]
        public Uri RepoUrl { get; set; } = new Uri("about:blank");
    
        [JsonPropertyName("targetRevision")]
        public string TargetRevision { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }


    public class BasicSource : SourceBase
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;
    }


    public class HelmSource : SourceBase
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;
    
        [JsonPropertyName("helm")]
        public HelmConfig Helm { get; set; } = new HelmConfig();
    }


    public class ReferenceSource : SourceBase
    {
        [JsonPropertyName("ref")]
        public string Ref { get; set; } = string.Empty;
    }
}

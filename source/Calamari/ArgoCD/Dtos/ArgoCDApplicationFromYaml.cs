using System;
using Newtonsoft.Json;

namespace Calamari.ArgoCD.Dtos
{
    public class ArgoCDApplicationFromYaml
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }
        
        [JsonProperty("spec")]
        public ArgoCDApplicationSpecFromYaml Spec { get; set; }

    }

    public class ArgoCDApplicationMetadataFromYaml
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("namespace")]
        public string Namespace { get; set; }
    }

    public class ArgoCDApplicationSpecFromYaml
    {
        [JsonProperty("source")]
        public ArgoCDApplicationSourceFromYaml Source { get; set; }
        
        [JsonProperty("sources")]
        public ArgoCDApplicationSourceFromYaml[] Sources { get; set; }

        public ArgoCDApplicationSourceFromYaml[] GetSourceList()
        {
            return Sources ?? new[] { Source };
        }
    }

    public class ArgoCDApplicationSourceFromYaml
    {
        [JsonProperty("repoURL")]
        public string RepoURL { get; set; }
        
        [JsonProperty("targetRevision")]
        public string TargetRevision { get; set; }
        
        [JsonProperty("path")]
        public string Path { get; set; }

    }
}
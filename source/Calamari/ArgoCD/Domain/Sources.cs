using System;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain
{
    public class ApplicationSource
    {
        readonly string originalRepoUrl = string.Empty;
        
        /// <summary>
        /// This property should not be used for accessing the repository used for Git interactions.
        /// Instead, use the <see cref="ForceParseRepoUrlAsHttp"/> method
        /// </summary>
        [JsonPropertyName("repoURL")]
        public string OriginalRepoUrl
        {
            get => originalRepoUrl;
            init
            {
                originalRepoUrl =  value;
                CloneSafeRepoUrl = GitCloneSafeUrl.FromString(OriginalRepoUrl);
            }
        }

        /// <summary>
        /// Use this Url when passing attempting to use it for git interactions.
        /// Context, we dont currently support SSH connections.
        /// </summary>
        public Uri CloneSafeRepoUrl  { get; private set;  }
    
        [JsonPropertyName("targetRevision")]
        public string TargetRevision { get; init; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("path")]
        public string? Path { get; init; }
        
        [JsonPropertyName("helm")]
        public HelmConfig? Helm { get; init; }
        
        [JsonPropertyName("ref")]
        public string? Ref { get; init; }
    }
}

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
                CloneSafeRepoUrl = ForceParseRepoUrlAsHttp(OriginalRepoUrl);
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
        
        /// <summary>
        /// If the <see cref="OriginalRepoUrl"/> is a ssh connection, the format is typically git@host...
        /// Note the missing schema. As such these cannot be parsed as a <see cref="Uri"/> object.
        /// Instead, to allow us to utilize credentials for HTTP git interaction, we attempt to convert the ssh address to a HTTPS one.
        /// </summary>
        /// <returns></returns>
        private static Uri ForceParseRepoUrlAsHttp(string originalUri)
        {

            const string expectedPrefix = "git@";
            if (!originalUri.StartsWith(expectedPrefix))
            {
                // If it doesn't look like ssh, lets just force a parse and
                // if it blows up we know there is some other protocols to cater for.
                return new Uri(originalUri);
            }

            var endpoint = originalUri.Substring(expectedPrefix.Length);
            var parts = endpoint.Split(":");
            var host = parts[0];
            var subdomain = parts[1];
            var builder = new UriBuilder("https", host)
            {
                Path = subdomain
            };
            return builder.Uri;
        }
    }
}

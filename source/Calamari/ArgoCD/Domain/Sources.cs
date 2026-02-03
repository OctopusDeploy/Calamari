using System;
using System.Text.Json.Serialization;

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

        public Uri ToUri()
        {
            return new Uri(RepoUrl);
        }
        
        /// <summary>
        /// If the <see cref="RepoUrl"/> is a ssh connection, the format is typically git@host...
        /// Note the missing schema. As such these cannot be parsed as a <see cref="Uri"/> object.
        /// Instead, to allow us to utilize credentials for HTTP git interaction, we attempt to convert the ssh address to a HTTPS one.
        /// </summary>
        /// <returns></returns>
        public Uri ForceParseRepoUrlAsHttp()
        {
            var originalUri = RepoUrl;
            const string expectedPrefix = "git@";
            if (!originalUri.StartsWith(expectedPrefix))
            {
                // If it doesn't look like ssh, lets just force a parse and
                // if it blows up we know there is some other protocols to cater for.
                return new Uri(RepoUrl);
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

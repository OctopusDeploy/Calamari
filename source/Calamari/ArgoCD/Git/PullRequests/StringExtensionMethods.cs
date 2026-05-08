using System;

namespace Calamari.ArgoCD.Git.PullRequests
{
    public static class StringExtensionMethods
    {
        public static string StripGitSuffix(this string url)
        {
            const string gitExtension = ".git";
            if (url.EndsWith(gitExtension, StringComparison.OrdinalIgnoreCase))
                return url.Substring(0, url.Length - gitExtension.Length);

            return url;
        }
        
        /// <summary>
        /// Parses a repository URL string into a <see cref="Uri"/>. Pull request clients require
        /// HTTPS URLs for REST API calls — SCP-style SSH URLs (e.g. git@host:path) are not valid URIs.
        /// The <see cref="GitVendorPullRequestClientResolver"/> guards against this by returning null
        /// for non-URI URLs, but this method provides a clear error if one slips through.
        /// </summary>
        public static Uri ParseAsHttpsUri(this string repositoryUrl)
        {
            if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException(
                    $"Pull request operations require an HTTPS repository URL, but got: '{repositoryUrl}'. " +
                    "SCP-style SSH URLs (e.g. git@github.com:org/repo.git) are not supported for pull request creation.");
            }

            return uri;
        }

        // This extension method is here until we can drop netfx and put it into the interface
        public static string[] ExtractPropertiesFromUrlPath(this Uri repositoryUri)
        {
            var parts = repositoryUri.AbsolutePath.TrimStart('/').StripGitSuffix().Split('/');
            return parts;
        }
    }
}
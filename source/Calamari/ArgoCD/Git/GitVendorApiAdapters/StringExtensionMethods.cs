using System;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
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
        
        // This extension method is here until we can drop netfx and put it into the interface
        public static string[] ExtractPropertiesFromUrlPath(this Uri repositoryUri)
        {
            var parts = repositoryUri.AbsolutePath.TrimStart('/').StripGitSuffix().Split('/');
            return parts;
        }
    }
}
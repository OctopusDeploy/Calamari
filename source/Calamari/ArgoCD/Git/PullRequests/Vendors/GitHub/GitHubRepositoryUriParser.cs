using System;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.GitHub
{
    public static class GitHubRepositoryUriParser
    {
        public static (string Owner, string Repository) ParseOwnerAndRepository(Uri uri)
        {
            if (!IsGitHub(uri))
            {
                throw new InvalidOperationException("The repository URL does not point to a GitHub repository");
            }

            var parts = uri.SplitPathIntoSegments();

            if (parts.Length < 2)
            {
                throw new InvalidOperationException("The repository URL does not contain the repository owner and name");
            }

            return (parts[0], parts[1]);
        }

        public static bool IsGitHub(Uri uri)
        {
            return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                //Handle www.github.com
                ||
                uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase);
        }
    }
}

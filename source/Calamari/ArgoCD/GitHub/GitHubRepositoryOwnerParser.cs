using System;

namespace Calamari.ArgoCD.GitHub
{
    public static class GitHubRepositoryOwnerParser
    {
        public static (string Owner, string Repository) ParseOwnerAndRepository(Uri repoUrl)
        {
            if (!IsGitHub(repoUrl))
            {
                throw new InvalidOperationException("The repository URL does not point to a GitHub repository");
            }

            var pathParts = StripGitSuffix(repoUrl.AbsolutePath.TrimStart('/')).Split('/');

            if (pathParts.Length < 2)
            {
                throw new InvalidOperationException("The repository URL does not contain the repository owner and name");
            }

            return (pathParts[0], pathParts[1]);
        }

        //We only support github.com for now (Not GitHub Enterprise Server nor GHE.com)
        public static bool IsGitHub(Uri repoUrl)
        {
            return repoUrl.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                //Handle www.github.com
                || repoUrl.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase);
        }

        static string StripGitSuffix(string url)
        {
            const string gitExtension = ".git";
            if (url.EndsWith(gitExtension, StringComparison.OrdinalIgnoreCase))
                return url.Substring(0, url.Length - gitExtension.Length);

            return url;
        }
    }
}

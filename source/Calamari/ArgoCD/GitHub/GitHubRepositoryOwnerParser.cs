using System;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;

namespace Calamari.ArgoCD.GitHub
{
    public static class GitHubRepositoryOwnerParser
    {
        //We only support github.com for now (Not GitHub Enterprise Server nor GHE.com)
        public static bool IsGitHub(Uri repoUrl)
        {
            return repoUrl.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                //Handle www.github.com
                || repoUrl.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase);
        }

    }
}

using System;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Git
{
    public static class LibGit2SharpExtensionMethods
    {
        public static string ShortSha(this Commit commit)
        {
            return commit.Sha.Substring(0, 10);
        } 
    }
}

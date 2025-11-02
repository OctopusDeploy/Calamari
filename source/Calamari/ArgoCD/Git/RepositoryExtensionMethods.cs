#if NET
using System;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Git
{
    public static class RepositoryExtensionMethods
    {
        public static GitBranchName GetBranchName(this IRepository repository, GitReference gitReference)
        {
            return gitReference switch
                   {
                       GitHead _ => new GitBranchName(repository.Head.CanonicalName),
                       GitBranchName branchName => branchName,
                       _ => throw new ArgumentException("Unsupported GitReference type")
                   };
        }
    }
}
#endif
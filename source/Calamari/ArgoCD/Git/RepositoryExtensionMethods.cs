#if NET
using LibGit2Sharp;

namespace Calamari.ArgoCD.Git
{
    public static class RepositoryExtensionMethods
    {
        const string HeadAsTarget = "HEAD";
        
        public static string GetBranchName(this IRepository repository, GitBranchName branchName)
        {
            if (branchName.Value == HeadAsTarget)
            {
                return repository.Head.FriendlyName;
            }

            return branchName.Value;
        }
    }
}
#endif
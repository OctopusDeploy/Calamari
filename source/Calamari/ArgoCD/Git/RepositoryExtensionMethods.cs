using LibGit2Sharp;

namespace Calamari.ArgoCD.Git
{
    public static class RepositoryExtensionMethods
    {
        const string HeadAsTarget = "HEAD";
        
        public static string GetBranchName(this IRepository repository, string branchName)
        {
            if (branchName == HeadAsTarget)
            {
                return repository.Head.FriendlyName;
            }

            return branchName;
        }
    }
}
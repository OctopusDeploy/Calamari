using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Git
{
    public class GitBranchName : TinyType<string>
    {
        public GitBranchName(string value) : base(value)
        {
        }
    }
}
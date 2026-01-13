namespace Calamari.ArgoCD.Git
{
    public class GitCommitSha : GitReference
    {
        public GitCommitSha(string value) : base(value)
        {
        }
        
        public override string GetFriendlyName()
        {
            return Value;
        }
    }
}
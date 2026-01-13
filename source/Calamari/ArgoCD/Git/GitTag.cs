namespace Calamari.ArgoCD.Git
{
    public class GitTag : GitReference
    {
        public const string Prefix = "refs/tags/";

        public GitTag(string value) : base(value)
        {
        }
        
        public static GitBranchName CreateFromFriendlyName(string friendlyName)
        {
            return new GitBranchName($"{Prefix}{friendlyName}");
        }
        
        public override string GetFriendlyName()
        {
            if (Value.StartsWith(Prefix))
            {
                return Value.Substring(Prefix.Length);
            }

            return Value;
        }
    }
}
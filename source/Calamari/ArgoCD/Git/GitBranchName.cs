using System;

namespace Calamari.ArgoCD.Git
{
    public class GitBranchName : GitReference
    {
        public const string Prefix = "refs/heads/";

        public GitBranchName(string value) : base(value)
        {
            if (!Value.StartsWith(Prefix))
            {
                throw new InvalidCastException("Branch name must start with 'refs/heads/'");
            }
        }

        public static GitBranchName CreateFromFriendlyName(string friendlyName)
        {
            return new GitBranchName($"{Prefix}{friendlyName}");
        }
        
        public string ToFriendlyName()
        {
            if (Value.StartsWith(Prefix))
            {
                return Value.Substring(Prefix.Length);
            }

            return Value;
        }
    }
}
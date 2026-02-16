using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Git
{
    public abstract class GitReference : TinyType<string>
    {
        protected GitReference(string value) : base(value)
        {
        }

        public static GitReference CreateFromString(string value)
        {
            if (value == GitHead.HeadAsTarget)
            {
                return new GitHead();
            }

            return value.StartsWith(GitBranchName.Prefix) ?
                new GitBranchName(value) :
                GitBranchName.CreateFromFriendlyName(value);
        }
    }
}
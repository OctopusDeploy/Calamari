using System;

namespace Calamari.ArgoCD.Git
{
    public class GitHead : GitReference
    {
        public const string HeadAsTarget = "HEAD";

        public GitHead() : base(HeadAsTarget)
        {
        }
    }
}
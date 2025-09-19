using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Git
{
    public class GitCommitSummary : CaseSensitiveStringTinyType
    {
        public GitCommitSummary(string value) : base(value)
        {
        }
    }
}

using System;
using Octopus.TinyTypes;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages
{
    public class GitCommitSummary : CaseSensitiveStringTinyType
    {
        public GitCommitSummary(string value) : base(value)
        {
        }
    }
}

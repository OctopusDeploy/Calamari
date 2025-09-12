using System;

namespace Calamari.ArgoCD.Conventions
{
    public class GitCommitParameters
    {
        public string Summary { get; }
        public string Description { get; }
        public bool RequiresPr { get; }

        public GitCommitParameters(string summary, string description, bool requiresPr)
        {
            Summary = summary;
            Description = description;
            RequiresPr = requiresPr;
        }
    }
}
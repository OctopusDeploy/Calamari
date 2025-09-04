#nullable enable
using System.Collections.Generic;
using Calamari.ArgoCD.Git;

namespace Calamari.ArgoCD.Conventions
{
    public class ArgoCommitToGitConfig
    {
        public ArgoCommitToGitConfig(string workingDirectory, string inputSubPath, bool recurseInputPath, string commitSummary, string commitDescription,
                                   bool requiresPr,
                                   List<IArgoApplicationSource> argoSourcesToUpdate)
        {
            WorkingDirectory = workingDirectory;
            InputSubPath = inputSubPath;
            RecurseInputPath = recurseInputPath;
            CommitSummary = commitSummary;
            CommitDescription = commitDescription;
            RequiresPr = requiresPr;
            ArgoSourcesToUpdate = argoSourcesToUpdate;
        }
        
        public string WorkingDirectory { get; set; }

        public string[] FileGlobs => new[] { "*.yaml", "*.yml" };
        
        public string InputSubPath { get; }
        public bool RecurseInputPath { get; }
        
        public string CommitSummary { get; }
        public string? CommitDescription { get; }
        public bool RequiresPr { get; }
        public List<IArgoApplicationSource> ArgoSourcesToUpdate { get;  }
    }
}

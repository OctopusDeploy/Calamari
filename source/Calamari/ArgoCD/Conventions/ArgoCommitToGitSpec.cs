using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Git;

namespace Calamari.ArgoCD.Conventions
{
    public class ArgoCommitToGitSpec
    {
        public ArgoCommitToGitSpec(string workingDirectory, string inputSubPath, bool recurseInputPath, string commitSummary, string commitDescription,
                                   bool requiresPr,
                                   bool purgeOutputDirectory,
                                   List<IArgoApplicationSource> argoSourcesToUpdate)
        {
            WorkingDirectory = workingDirectory;
            InputSubPath = inputSubPath;
            RecurseInputPath = recurseInputPath;
            CommitSummary = commitSummary;
            CommitDescription = commitDescription;
            RequiresPr = requiresPr;
            PurgeOutputDirectory = purgeOutputDirectory;
            ArgoSourceToUpdate = argoSourcesToUpdate;
        }
        
        public string WorkingDirectory { get; set; }

        public string[] FileGlobs => new[] { "*.yaml", "*.yml" };
        
        public string InputSubPath { get; }
        public bool RecurseInputPath { get; }
        
        public string CommitSummary { get; }
        public string CommitDescription { get; }
        public bool RequiresPr { get; }
        public bool PurgeOutputDirectory { get; }

        public List<IArgoApplicationSource> ArgoSourceToUpdate { get;  }
    }
}
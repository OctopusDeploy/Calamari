#if NET
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions
{
    public class ArgoCommitToGitConfig
    {
        public ArgoCommitToGitConfig(string workingDirectory, string inputSubPath, bool recurseInputPath, GitCommitParameters commitParameters)
        {
            WorkingDirectory = workingDirectory;
            InputSubPath = inputSubPath;
            RecurseInputPath = recurseInputPath;
            CommitParameters = commitParameters;
        }
        
        public string WorkingDirectory { get; set; }

        public string[] FileGlobs => new[] { "*.yaml", "*.yml" };
        
        public string InputSubPath { get; }
        public bool RecurseInputPath { get; }
        public GitCommitParameters CommitParameters { get; }
    }
}
#endif
#if NET
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions
{
    public class ArgoCommitToGitConfig
    {
        public ArgoCommitToGitConfig(string workingDirectory, string inputSubPath, bool purgeOutputDirectory, GitCommitParameters commitParameters)
        {
            WorkingDirectory = workingDirectory;
            InputSubPath = inputSubPath;
            PurgeOutputDirectory = purgeOutputDirectory;
            CommitParameters = commitParameters;
        }
        
        public string WorkingDirectory { get; }
        
        public string InputSubPath { get; }
        public bool PurgeOutputDirectory { get; }
        public GitCommitParameters CommitParameters { get; }
    }
}
#endif

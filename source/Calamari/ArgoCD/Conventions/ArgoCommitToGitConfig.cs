namespace Calamari.ArgoCD.Conventions
{
    public interface IGitCommitParameters
    {
        public string CommitSummary { get; }
        public string CommitDescription { get; }
        public bool RequiresPr { get; }
    }
    
    public class ArgoCommitToGitConfig : IGitCommitParameters
    {
        public ArgoCommitToGitConfig(string workingDirectory, string inputSubPath, bool recurseInputPath, string commitSummary, string commitDescription,
                                   bool requiresPr)
        {
            WorkingDirectory = workingDirectory;
            InputSubPath = inputSubPath;
            RecurseInputPath = recurseInputPath;
            CommitSummary = commitSummary;
            CommitDescription = commitDescription;
            RequiresPr = requiresPr;
        }
        
        public string WorkingDirectory { get; set; }

        public string[] FileGlobs => new[] { "*.yaml", "*.yml" };
        
        public string InputSubPath { get; }
        public bool RecurseInputPath { get; }
        
        public string CommitSummary { get; }
        public string CommitDescription { get; }
        public bool RequiresPr { get; }
    }
}

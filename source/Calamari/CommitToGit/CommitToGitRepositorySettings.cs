using System;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;

namespace Calamari.CommitToGit
{
    public class CommitToGitRepositorySettings
    {
        public CommitToGitRepositorySettings(GitConnection gitConnection, GitCommitParameters commitParameters, string destinationPath)
        {
            GitConnection = gitConnection;
            DestinationPath = destinationPath;
            CommitParameters = commitParameters;
        }

        public GitConnection GitConnection { get; }

        public string DestinationPath { get; }
        public GitCommitParameters CommitParameters { get; }
    }
}


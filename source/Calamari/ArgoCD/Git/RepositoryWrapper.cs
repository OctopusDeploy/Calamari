using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Plumbing.Logging;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Git
{
    public class RepositoryWrapper
    {
        readonly IRepository repository;
        readonly ILog log;
        readonly IGitConnection connection;
        readonly IGitHubPullRequestCreator pullRequestCreator;

        public string WorkingDirectory => repository.Info.WorkingDirectory;

        public RepositoryWrapper(IRepository repository, ILog log, IGitConnection connection, IGitHubPullRequestCreator pullRequestCreator)
        {
            this.repository = repository;
            this.log = log;
            this.connection = connection;
            this.pullRequestCreator = pullRequestCreator;
        }
        
        
        // returns true if changes were made to the repository
        public bool CommitChanges(string summary, string description)
        {
            var fullMessage = GenerateCommitMessage(summary, description);
            return CommitChanges(fullMessage);
        }

        public bool CommitChanges(string fullMessage)
        {
            try
            {
                var commitTime = DateTimeOffset.Now;
                
                var commit = repository.Commit(fullMessage,
                                               new Signature("Octopus", "octopus@octopus.com", commitTime),
                                               new Signature("Octopus", "octopus@octopus.com", commitTime));
                log.Verbose($"Committed changes to {commit.Sha}");
                return true;
            }
            catch (EmptyCommitException)
            {
                return false;
            } 
        }
        
        public void StageFiles(string[] filesToStage)
        {
            //find files which have changed in fs??? <---   
            foreach (var file in filesToStage)
            {
                var fileToAdd = file.StartsWith("./") ? file.Substring(2) : file;
                // if a file does not exist - what should we do? throw and continue? or just throw?
                repository.Index.Add(fileToAdd);
            }
        }
        
        public async Task PushChanges(bool requiresPullRequest, GitBranchName branchName, CancellationToken cancellationToken)
        {
            var currentBranchName = repository.GetBranchName(branchName);
            var pushToBranchName = currentBranchName;
            if (requiresPullRequest)
            {
                pushToBranchName = CalculateBranchName();
            }
            Log.Info($"Pushing changes to branch '{pushToBranchName}'");
            PushChanges(pushToBranchName);
            if (requiresPullRequest)
            {
                var commit = repository.Head.Tip; //this is a BIT dodgy - as it assumes we're pushing head.
                var commitSummary = commit.MessageShort;
                var commitDescription = commit.Message.Substring(commitSummary.Length).Trim('\n');
                await pullRequestCreator.CreatePullRequest(log, connection, commitSummary, commitDescription, new GitBranchName(pushToBranchName),  new GitBranchName(currentBranchName), cancellationToken);
            }
        }

        string CalculateBranchName()
        {
            return $"octopus-argo-cd-{Guid.NewGuid().ToString("N").Substring(0, 10)}";
        }
        
        public void PushChanges(string branchName)
        {
            Remote remote = repository.Network.Remotes["origin"];
            repository.Branches.Update(repository.Head, 
                                       branch => branch.Remote = remote.Name,
                                       branch => branch.UpstreamBranch = $"refs/heads/{branchName}");
            
            log.Info($"Pushing changes to branch '{branchName}' with original credentials");
            var pushOptions = new PushOptions
            {
                CredentialsProvider = (url, usernameFromUrl, types) =>
                                          new UsernamePasswordCredentials { Username = connection.Username, Password = connection.Password }
            };
            
            repository.Network.Push(repository.Head, pushOptions);
        }
        
        string GenerateCommitMessage(string summary, string description)
        {
            return description.Equals(string.Empty)
                ? summary
                : $"{summary}\n\n{description}";
        }
    }
}

#if NET
using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Plumbing.Logging;
using LibGit2Sharp;
using NuGet.Commands;

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
            try
            {
                var commitTime = DateTimeOffset.Now;
                var commitMessage = GenerateCommitMessage(summary, description);
                var commit = repository.Commit(commitMessage,
                                               new Signature("Octopus", "octopus@octopus.com", commitTime),
                                               new Signature("Octopus", "octopus@octopus.com", commitTime));
                log.Verbose($"Committed changes to {commit.Sha}");
                return true;
            }
            catch (EmptyCommitException)
            {
                log.Verbose("No changes required committing.");
                return false;
            }
        }
        
        public void StageFiles(string[] filesToStage)
        {
            foreach (var file in filesToStage)
            {
                var fileToAdd = file.StartsWith("./") ? file.Substring(2) : file;
                repository.Index.Add(fileToAdd);
            }
        }
        
        public async Task PushChanges(bool requiresPullRequest, string summary, string description, GitBranchName branchName, CancellationToken cancellationToken)
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
                await pullRequestCreator.CreatePullRequest(log, connection, summary, description, new GitBranchName(pushToBranchName),  new GitBranchName(currentBranchName), cancellationToken);
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

            PushStatusError? errorsDetected = null;
            var pushOptions = new PushOptions
            {
                CredentialsProvider = (url, usernameFromUrl, types) =>
                                          new UsernamePasswordCredentials { Username = connection.Username, Password = connection.Password },
                OnPushStatusError = errors => errorsDetected = errors
            };
            
            repository.Network.Push(repository.Head, pushOptions);
            if (errorsDetected != null)
            {
                throw new CommandException($"Failed to push to branch {branchName} - {errorsDetected.Message}");
            }
        }
        
        string GenerateCommitMessage(string summary, string description)
        {
            return description.Equals(string.Empty)
                ? summary
                : $"{summary}\n\n{description}";
        }
    }
}
#endif

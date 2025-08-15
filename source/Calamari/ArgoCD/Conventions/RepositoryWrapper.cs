using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Logging;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Conventions
{
    public class RepositoryWrapper
    {
        readonly IRepository repository;
        readonly ILog log;
        
        public string WorkingDirectory => repository.Info.WorkingDirectory;

        public RepositoryWrapper(IRepository repository, ILog log)
        {
            this.repository = repository;
            this.log = log;
        }
        

        // returns true if changes were made to the repository
        public bool CommitChanges(string commitMessage)
        {
            try
            {
                var commitTime = DateTimeOffset.Now;
                repository.Commit(commitMessage,
                                  new Signature("Octopus", "octopus@octopus.com", commitTime),
                                  new Signature("Octopus", "octopus@octopus.com", commitTime));
                return true;
            }
            catch (EmptyCommitException)
            {
                return false;
            }
        }
        
        public void StageFiles(List<string> filesToStage)
        {
            foreach (var file in filesToStage)
            {
                repository.Index.Add(file);
            }
        }
        
        public void PushChanges(bool requiresPullRequest, string branchName)
        {
            var pushToBranchName = branchName;
            if (requiresPullRequest)
            {
                pushToBranchName += "-pullrequest";
            }
            Log.Info($"Pushing changes to branch '{pushToBranchName}'");
            PushChanges(pushToBranchName);
            if (requiresPullRequest)
            {
                //perform the pull request creation work.
            }
        }
        
        public void PushChanges(string branchName)
        {
            Remote remote = repository.Network.Remotes["origin"];
            repository.Branches.Update(repository.Head, 
                                       branch => branch.Remote = remote.Name,
                                       branch => branch.UpstreamBranch = $"refs/heads/{branchName}");
            
            repository.Network.Push(repository.Head);
        }
    }
}
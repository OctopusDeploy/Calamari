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
        readonly IGitConnection connection;

        public string WorkingDirectory => repository.Info.WorkingDirectory;

        public RepositoryWrapper(IRepository repository, ILog log, IGitConnection connection)
        {
            this.repository = repository;
            this.log = log;
            this.connection = connection;
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
                //WHAT THE HELL?! libGit2Sharp doesn't like the "./" at the start of the relative path
                //which comes from the ARGOCD-SUBFOLDER
                repository.Index.Add(file.Substring(2));
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
                                       branch => branch.UpstreamBranch = $"refs/heads/main"); //TODO(tmm): HACK THIS BACK TO branchName
            
            log.Info($"Pushing changes to branch '{branchName}' with original credentials");
            var pushOptions = new PushOptions
            {
                CredentialsProvider = (url, usernameFromUrl, types) =>
                                          new UsernamePasswordCredentials { Username = connection.Username, Password = connection.Password }
            };
            
            repository.Network.Push(repository.Head, pushOptions);
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Conventions
{
    public static class RepositoryHelpers
    {
        public static Repository CloneRepository(string repositoryPath, IGitConnection repositoryBranchFolder)
        {
            Directory.CreateDirectory(repositoryPath);
            return CheckoutGitRepository(repositoryBranchFolder, repositoryPath);            
        }
        
        static Repository CheckoutGitRepository(IGitConnection gitConnection, string checkoutPath)
        {
            //Todo - cannot make this work
            // var options = new CloneOptions
            // {
            //     BranchName = gitConnection.BranchName
            // };

            var options = new CloneOptions();
            if (gitConnection.Username != null && gitConnection.Password != null)
            {
                options.FetchOptions = new FetchOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) => new UsernamePasswordCredentials
                    {
                        Username = gitConnection.Username!,
                        Password = gitConnection.Password!
                    }
                };
            }

            var repoPath = Repository.Clone(gitConnection.Url, checkoutPath, options);
            var repo = new Repository(repoPath);
            Branch remoteBranch = repo.Branches[gitConnection.RemoteBranchName];
            
            //A local branch is required such that libgit2sharp can create "tracking" data
            // libgit2sharp does not support pushing from a detached head
            repo.CreateBranch(gitConnection.BranchName, remoteBranch.Tip);
            LibGit2Sharp.Commands.Checkout(repo, gitConnection.BranchName);
            return repo;
        }
        
        public static void PushChanges(string branchName, Repository repo)
        {
            repo.Commit("Updated the git repo",
                        new Signature("Octopus", "octopus@octopus.com", DateTimeOffset.Now),
                        new Signature("Octopus", "octopus@octopus.com", DateTimeOffset.Now));
            
            Remote remote = repo.Network.Remotes["origin"];
            repo.Branches.Update(repo.Head, 
                                 branch => branch.Remote = remote.Name,
                                 branch => branch.UpstreamBranch = $"refs/heads/{branchName}");
            
            repo.Network.Push(repo.Head);
        }

        public static void StageFiles(List<string> filesToStage, Repository repo)
        {
            foreach (var file in filesToStage)
            {
                repo.Index.Add(file);
            }
        }
    }
}
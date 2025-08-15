using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Conventions
{
    public interface IRepositoryFactory
    {
        RepositoryWrapper CloneRepository(string repositoryName, IGitConnection gitConnection);
    }
    
    public class RepositoryFactory : IRepositoryFactory
    {
        readonly ILog log;
        readonly string repositoryParentDirectory;

        public RepositoryFactory(ILog log, string repositoryParentDirectory)
        {
            this.log = log;
            this.repositoryParentDirectory = repositoryParentDirectory;
        }

        public RepositoryWrapper CloneRepository(string repositoryName, IGitConnection gitConnection)
        {
            Log.Info($"Cloning repository {gitConnection.Url}, checking out branch '{gitConnection.BranchName}'");
            var repositoryPath = Path.Combine(repositoryParentDirectory, repositoryName);
            Directory.CreateDirectory(repositoryPath);
            return CheckoutGitRepository(gitConnection, repositoryPath);            
        }
        
        RepositoryWrapper CheckoutGitRepository(IGitConnection gitConnection, string checkoutPath)
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
            if (repo.Branches[gitConnection.BranchName] == null)
            {
                repo.CreateBranch(gitConnection.BranchName, remoteBranch.Tip);    
            }
            LibGit2Sharp.Commands.Checkout(repo, gitConnection.BranchName);

            return new RepositoryWrapper(repo, log);
        }
    }
}
#if NET
using System;
using System.IO;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Plumbing.Logging;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Git
{
    public interface IRepositoryFactory
    {
        RepositoryWrapper CloneRepository(string repositoryName, IGitConnection gitConnection);
    }

    public class RepositoryFactory : IRepositoryFactory
    {
        readonly ILog log;
        readonly string repositoryParentDirectory;
        readonly IGitHubPullRequestCreator pullRequestCreator;

        public RepositoryFactory(ILog log, string repositoryParentDirectory, IGitHubPullRequestCreator gitHubPullRequestCreator)
        {
            this.log = log;
            this.repositoryParentDirectory = repositoryParentDirectory;
            pullRequestCreator = gitHubPullRequestCreator;
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
                options.FetchOptions.CredentialsProvider = (url, usernameFromUrl, types) => new UsernamePasswordCredentials
                {
                    Username = gitConnection.Username!,
                    Password = gitConnection.Password!
                };
            }

            var repoPath = Repository.Clone(gitConnection.Url, checkoutPath, options);
            var repo = new Repository(repoPath);

            //this is required to handle the issue around "HEAD"
            var branchToCheckout = repo.GetBranchName(gitConnection.BranchName);
            Branch remoteBranch = repo.Branches[$"origin/{branchToCheckout}"];
            log.Verbose($"Checking out {remoteBranch.Tip.Sha}");

            //A local branch is required such that libgit2sharp can create "tracking" data
            // libgit2sharp does not support pushing from a detached head
            if (repo.Branches[branchToCheckout] == null)
            {
                repo.CreateBranch(branchToCheckout, remoteBranch.Tip);
            }

            LibGit2Sharp.Commands.Checkout(repo, branchToCheckout);

            return new RepositoryWrapper(repo, log, gitConnection, pullRequestCreator);
        }
    }
}
#endif
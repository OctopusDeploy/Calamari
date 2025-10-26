#if NET
using System;
using System.IO;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
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
        readonly ICalamariFileSystem fileSystem;
        readonly string repositoryParentDirectory;
        readonly IGitHubPullRequestCreator pullRequestCreator;

        public RepositoryFactory(ILog log, ICalamariFileSystem fileSystem, string repositoryParentDirectory, IGitHubPullRequestCreator gitHubPullRequestCreator)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.repositoryParentDirectory = repositoryParentDirectory;
            this.pullRequestCreator = gitHubPullRequestCreator;
        }

        public RepositoryWrapper CloneRepository(string repositoryName, IGitConnection gitConnection)
        {
            var repositoryPath = Path.Combine(repositoryParentDirectory, repositoryName);
            fileSystem.CreateDirectory(repositoryPath);

            return CheckoutGitRepository(gitConnection, repositoryPath);
        }

        RepositoryWrapper CheckoutGitRepository(IGitConnection gitConnection, string checkoutPath)
        {
            //if the branch name is head, then we just clone the default
            //if it's not head, then clone the branch immediately
            var options = gitConnection.BranchName.Value.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
                ? new CloneOptions()
                : new CloneOptions
                {
                    BranchName = gitConnection.BranchName.Value
                };

            if (gitConnection.Username != null && gitConnection.Password != null)
            {
                options.FetchOptions.CredentialsProvider = (url, usernameFromUrl, types) => new UsernamePasswordCredentials
                {
                    Username = gitConnection.Username!,
                    Password = gitConnection.Password!
                };
            }

            string repoPath;
            log.InfoFormat("Cloning repository {0}", log.FormatLink(gitConnection.Url));
            using (var timedOp = log.BeginTimedOperation("cloning repository"))
            {
                try
                {
                    repoPath = Repository.Clone(gitConnection.Url, checkoutPath, options);
                    timedOp.Complete();
                }
                catch (Exception e)
                {
                    timedOp.Abandon(e);
                    throw new CommandException($"Failed to clone Git repository at {gitConnection.Url}. Are you sure this is a Git repository?");
                }
            }

            var repo = new Repository(repoPath);

            //this is required to handle the issue around "HEAD"
            var branchToCheckout = repo.GetBranchName(gitConnection.BranchName);
            var remoteBranch = repo.Branches[$"origin/{branchToCheckout}"];
            
            log.VerboseFormat("Checking out '{0}' @ {1}", branchToCheckout, remoteBranch.Tip.Sha.Substring(0, 10));
            
            //A local branch is required such that libgit2sharp can create "tracking" data
            // libgit2sharp does not support pushing from a detached head
            if (repo.Branches[branchToCheckout] == null)
            {
                repo.CreateBranch(branchToCheckout, remoteBranch.Tip);
            }
            
            LibGit2Sharp.Commands.Checkout(repo, branchToCheckout);

            return new RepositoryWrapper(repo,
                                         fileSystem,
                                         checkoutPath,
                                         log,
                                         gitConnection,
                                         pullRequestCreator);
        }
    }
}
#endif
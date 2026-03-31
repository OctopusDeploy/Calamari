using System;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Time;
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
        readonly IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver;
        readonly IClock clock;

        public RepositoryFactory(ILog log, ICalamariFileSystem fileSystem, string repositoryParentDirectory, IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver,
                                 IClock clock)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.repositoryParentDirectory = repositoryParentDirectory;
            this.gitVendorPullRequestClientResolver = gitVendorPullRequestClientResolver;
            this.clock = clock;

            // Calamari runs as a single-purpose process per deployment step and always receives
            // explicit credentials. Clear the search paths for all global config levels so libgit2
            // cannot load ~/.gitconfig or /etc/gitconfig and pick up a credential helper (e.g.
            // osxkeychain) that would silently override or bypass the credentials we provide.
            GlobalSettings.SetConfigSearchPaths(ConfigurationLevel.Global, []);
            GlobalSettings.SetConfigSearchPaths(ConfigurationLevel.System, []);
            GlobalSettings.SetConfigSearchPaths(ConfigurationLevel.Xdg, []);
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
            var options = gitConnection.GitReference is GitHead
                ? new CloneOptions()
                : new CloneOptions
                {
                    //note: when cloning, libgit2sharp prepends "refs/remotes/origin/" to this value (so _must_ be a branch to succeed).
                    BranchName = (gitConnection.GitReference as GitBranchName)?.ToFriendlyName()
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
                    repoPath = Repository.Clone(gitConnection.Url.AbsoluteUri, checkoutPath, options);
                    timedOp.Complete();
                }
                catch (Exception e)
                {
                    timedOp.Abandon(e);
                    log.Error("Cloning repository failed");
                    log.Verbose(e.PrettyPrint());
                    throw new CommandException($"Failed to clone Git repository at {gitConnection.Url}. Are you sure this URL is a Git repository, and the reference is a branch?", e);
                }
            }

            var repo = new Repository(repoPath);

            //this is required to handle the issue around "HEAD"
            var branchToCheckout = repo.GetBranchName(gitConnection.GitReference);
            var remoteBranch = repo.Branches.First(f => f.IsRemote && f.UpstreamBranchCanonicalName == branchToCheckout.Value);
            
            log.VerboseFormat("Checking out '{0}' @ {1}", branchToCheckout, remoteBranch.Tip.Sha.Substring(0, 10));
            
            //A local branch is required such that libgit2sharp can create "tracking" data
            // libgit2sharp does not support pushing from a detached head
            if (repo.Branches[branchToCheckout.Value] == null)
            {
                repo.CreateBranch(branchToCheckout.Value, remoteBranch.Tip);
            }
            
            LibGit2Sharp.Commands.Checkout(repo, branchToCheckout.ToFriendlyName());

            //TODO(tmm): Is this an acceptable way to call an async function?
            var gitVendorApiAdapter = gitVendorPullRequestClientResolver.TryResolve(gitConnection, log, CancellationToken.None).Result;
            return new RepositoryWrapper(repo,
                                         fileSystem,
                                         checkoutPath,
                                         log,
                                         gitConnection,
                                         gitVendorApiAdapter,
                                         clock);
        }
    }
}

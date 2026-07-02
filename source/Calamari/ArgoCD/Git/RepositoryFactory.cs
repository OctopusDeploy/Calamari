using System;
using System.IO;
using System.Threading;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
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

            LibGit2SharpTransportRegistration.EnsureRegistered();

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
            WindowsSshKeys.AssertSupported(gitConnection);

            var repositoryPath = Path.Combine(repositoryParentDirectory, repositoryName);
            fileSystem.CreateDirectory(repositoryPath);

            return CheckoutGitRepository(gitConnection, repositoryPath);
        }

        RepositoryWrapper CheckoutGitRepository(IGitConnection gitConnection, string checkoutPath)
        {
            // Always clone with default options so the remote's default branch is checked out first. This lets
            // the RepositoryWrapper capture the true default branch (for resolving 'HEAD' references) before we
            // check out the requested reference, and leaves every remote branch available so the clone can be
            // reused to check out additional branches without re-cloning.
            var options = new CloneOptions();
            options.FetchOptions.CredentialsProvider = gitConnection.ToLibGit2SharpCredentialHandler();
            options.FetchOptions.CertificateCheck = gitConnection.ToLibGit2SharpCertificateCheckHandler(log);

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
                    log.Error("Cloning repository failed");
                    log.Verbose(e.PrettyPrint());
                    throw new CommandException($"Failed to clone Git repository at {gitConnection.Url}. Are you sure this URL is a Git repository, and the reference is a branch?", e);
                }
            }

            var repo = new Repository(repoPath);

            //TODO(tmm): Make this function (and all callers async).
            var gitVendorApiAdapter = gitConnection is HttpsGitConnection httpsGitConnection
                ? gitVendorPullRequestClientResolver.TryResolve(httpsGitConnection, log, CancellationToken.None).Result
                : null;

            if (gitConnection is SshKeyGitConnection)
            {
                log.Verbose("Git is using SSH authentication, Git vendor functionality such as PR creation will not be available");
            }

            var repository = new RepositoryWrapper(repo,
                                                   fileSystem,
                                                   checkoutPath,
                                                   log,
                                                   gitConnection,
                                                   gitVendorApiAdapter,
                                                   clock);

            try
            {
                repository.CheckoutBranch(gitConnection.GitReference);
            }
            catch (Exception e)
            {
                repository.Dispose();
                // Preserve the original behaviour where requesting a reference that is not a branch surfaces as a clone failure.
                throw new CommandException($"Failed to clone Git repository at {gitConnection.Url}. Are you sure this URL is a Git repository, and the reference is a branch?", e);
            }

            return repository;
        }
    }
}

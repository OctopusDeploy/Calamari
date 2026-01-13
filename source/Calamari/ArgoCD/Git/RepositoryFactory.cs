#if NET
using System;
using System.IO;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using System.Linq;
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
        readonly IGitVendorAgnosticApiAdapterFactory vendorAgnosticApiAdapterFactory;

        public RepositoryFactory(ILog log, ICalamariFileSystem fileSystem, string repositoryParentDirectory, IGitVendorAgnosticApiAdapterFactory vendorAgnosticApiAdapterFactory)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.repositoryParentDirectory = repositoryParentDirectory;
            this.vendorAgnosticApiAdapterFactory = vendorAgnosticApiAdapterFactory;
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
            var options = gitConnection.GitReference.Equals(GitHead.HeadAsTarget)
                ? new CloneOptions()
                : new CloneOptions
                {
                    BranchName = gitConnection.GitReference //NOTE: this string reference could be a branch, commit, or tag
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
                    throw new CommandException($"Failed to clone Git repository at {gitConnection.Url}. Are you sure this is a Git repository? {e.Message}", e);
                }
            }

            var repo = new Repository(repoPath);
            var gitReferenceFactory =  new GitReferenceFactory(repo);
            var targetReference = gitReferenceFactory.CreateGitReference(gitConnection.GitReference);
            
            //this is required to handle the issue around "HEAD"targetReference
            var remoteBranch = repo.Branches.First(f => f.IsRemote && f.UpstreamBranchCanonicalName == targetReference.Value);
            
            log.VerboseFormat("Checking out '{0}' @ {1}", targetReference, remoteBranch.Tip.Sha.Substring(0, 10));
            
            //A local branch is required such that libgit2sharp can create "tracking" data
            // libgit2sharp does not support pushing from a detached head
            if (repo.Branches[targetReference.Value] == null)
            {
                repo.CreateBranch(targetReference.Value, remoteBranch.Tip);
            }
            
            LibGit2Sharp.Commands.Checkout(repo, targetReference.GetFriendlyName());
            
            var gitVendorApiAdapter = vendorAgnosticApiAdapterFactory.TryCreateGitVendorApiAdaptor(gitConnection);
            return new RepositoryWrapper(repo,
                                         fileSystem,
                                         checkoutPath,
                                         log,
                                         gitConnection,
                                         gitVendorApiAdapter);
        }
    }
}
#endif
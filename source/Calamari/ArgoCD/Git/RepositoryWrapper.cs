using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Time;
using LibGit2Sharp;
using Polly;
using Polly.Retry;

namespace Calamari.ArgoCD.Git
{
    public class RepositoryWrapper(
        Repository repository,
        ICalamariFileSystem calamariFileSystem,
        string repoCheckoutDirectoryPath,
        ILog log,
        IGitConnection connection,
        IGitVendorPullRequestClient? vendorApiAdapter,
        IClock clock)
        : IDisposable
    {
        // ReSharper disable ReplaceWithPrimaryConstructorParameter - This makes parameters readonly
        readonly Repository repository = repository;
        readonly ICalamariFileSystem calamariFileSystem = calamariFileSystem;
        readonly string repoCheckoutDirectoryPath = repoCheckoutDirectoryPath;
        readonly ILog log = log;
        readonly IGitConnection connection = connection;
        readonly IGitVendorPullRequestClient? vendorApiAdapter = vendorApiAdapter;
        readonly IClock clock = clock;
        // ReSharper restore ReplaceWithPrimaryConstructorParameter

        readonly Identity repositoryIdentity = new("Octopus", "octopus@octopus.com");

        // Captured at construction (immediately after clone, before any checkout) so that 'HEAD'
        // references can be resolved to the remote's default branch even after we have checked out
        // a different branch on this (reused) clone.
        readonly string defaultBranchCanonicalName = repository.Head.CanonicalName;

        public string WorkingDirectory => repository.Info.WorkingDirectory;

        // Checks out the requested reference, creating a local tracking branch if required, and hard-resets
        // it to the remote tip. Safe to call repeatedly on a single clone to switch between branches (or to
        // reset the current branch back to the remote tip between sources when raising pull requests).
        public void CheckoutBranch(GitReference reference)
        {
            var branchToCheckout = reference is GitHead
                ? new GitBranchName(defaultBranchCanonicalName)
                : repository.GetBranchName(reference);

            var remoteBranch = repository.Branches.FirstOrDefault(f => f.IsRemote && f.UpstreamBranchCanonicalName == branchToCheckout.Value);
            if (remoteBranch == null)
            {
                throw new CommandException($"Failed to checkout branch '{reference}' in repository at {connection.Url}. The reference could not be found as a branch on the remote.");
            }

            try
            {
                log.VerboseFormat("Checking out '{0}' @ {1}", branchToCheckout, remoteBranch.Tip.Sha.Substring(0, 10));

                //A local branch is required such that libgit2sharp can create "tracking" data
                // libgit2sharp does not support pushing from a detached head
                if (repository.Branches[branchToCheckout.Value] == null)
                {
                    repository.CreateBranch(branchToCheckout.Value, remoteBranch.Tip);
                }

                LibGit2Sharp.Commands.Checkout(repository, branchToCheckout.ToFriendlyName());
                // Ensure the local branch matches the remote tip. This matters when the clone is reused:
                // a previous source may have left a local commit on this branch that must not leak into this one.
                repository.Reset(ResetMode.Hard, remoteBranch.Tip);
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Failed to checkout branch '{reference}' in repository at {connection.Url}. Error: {e.Message}", e);
            }
        }

        // Returns the current HEAD commit as a PushResult. Used to capture a per-application commit before
        // a single push of all the application commits made on a branch.
        public PushResult GetHeadCommitResult()
        {
            var commit = repository.Head.Tip;
            return new PushResult(commit.Sha, commit.ShortSha(), commit.Author.When);
        }

        // The set of files that currently differ from HEAD (modified, added, deleted, untracked). Used to
        // detect whether a particular source actually changed anything: a source updater may produce a result
        // (e.g. a computed image patch) that is identical to what is already committed, in which case it must
        // not be attributed a commit. Mirrors git's "nothing to commit" behaviour at a per-source granularity.
        public IReadOnlyCollection<string> GetChangedFilePaths()
        {
            var status = repository.RetrieveStatus(new StatusOptions { IncludeUntracked = true, RecurseUntrackedDirs = true, IncludeIgnored = false });
            return status.Where(e => e.State != FileStatus.Unaltered && e.State != FileStatus.Ignored)
                         .Select(e => e.FilePath)
                         .ToList();
        }

        // returns true if changes were made to the repository
        public bool CommitChanges(string summary, string description)
        {
            try
            {
                var commitTime = clock.GetUtcTime();
                var commitMessage = GenerateCommitMessage(summary, description);
                var commit = repository.Commit(commitMessage,
                                               new Signature(repositoryIdentity, commitTime),
                                               new Signature(repositoryIdentity, commitTime));
                log.Verbose($"Committed changes to {commit.ShortSha()}");
                return true;
            }
            catch (EmptyCommitException)
            {
                log.Verbose("No changes required committing.");
                return false;
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Failed to commit changes to git repository. Error: {e.Message}", e);
            }
        }

        public void StageAllChanges()
        {
            try
            {
                LibGit2Sharp.Commands.Stage(repository, "*");
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Failed to stage files in git repository. Error: {e.Message}", e);
            }
        }

        public async Task<PushResult> PushChanges(bool requiresPullRequest,
                                                  string summary,
                                                  string description,
                                                  GitReference branchName,
                                                  int maxRetryAttempts,
                                                  CancellationToken cancellationToken)
        {
            var currentBranchName = repository.GetBranchName(branchName);
            var pushToBranchName = requiresPullRequest ? CalculateBranchName() : currentBranchName;

            log.Info($"Pushing changes to branch '{pushToBranchName.ToFriendlyName()}'");

            // Polly rejects a retry strategy with MaxRetryAttempts < 1
            var retryPipeline = maxRetryAttempts <= 0
                ? ResiliencePipeline.Empty
                : new ResiliencePipelineBuilder()
                  .AddRetry(new RetryStrategyOptions
                  {
                      ShouldHandle = new PredicateBuilder().Handle<CommandException>().Handle<NonFastForwardException>(),
                      MaxRetryAttempts = maxRetryAttempts,
                      UseJitter =  true,
                      Delay = TimeSpan.FromSeconds(2),
                      OnRetry = args =>
                      {
                          log.Verbose($"Push to '{pushToBranchName.ToFriendlyName()}' failed (attempt {args.AttemptNumber + 1}), fetching and rebasing before retrying");
                          FetchAndRebase(currentBranchName);
                          return default;
                      }
                  })
                  .Build();

            try
            {
                retryPipeline.Execute(() => PushChanges(pushToBranchName));
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Failed to push to branch '{pushToBranchName.ToFriendlyName()}'. Error: {e.Message}", e);
            }

            var commit = repository.Head.Tip;

            if (vendorApiAdapter != null)
            {
                var url = vendorApiAdapter.GenerateCommitUrl(commit.Sha);
                log.Info($"Commit {log.FormatLink(url, commit.ShortSha())} pushed");
            }
            else
            {
                log.Info($"Commit {commit.ShortSha()} pushed");
            }

            if (!requiresPullRequest)
            {
                return new PushResult(commit.Sha, commit.ShortSha(), commit.Author.When);
            }

            var ((title, number, uri), vendorName) = await CreatePullRequest(
                summary,
                description,
                pushToBranchName,
                currentBranchName,
                cancellationToken);

            return new PullRequestPushResult(
                commit.Sha,
                commit.ShortSha(),
                commit.Author.When,
                connection.Url,
                title,
                uri,
                number,
                vendorName);
        }

        async Task<(PullRequest PullRequest, string VendorName)> CreatePullRequest(
            string summary,
            string description,
            GitBranchName pushToBranchName,
            GitBranchName currentBranchName,
            CancellationToken cancellationToken)
        {
            if (vendorApiAdapter == null)
            {
                throw new CommandException("No Git provider can be resolved based on the provided repository details");
            }

            try
            {
                log.Verbose($"Attempting to create pull request to {connection.Url}");
                var pullRequest = await vendorApiAdapter.CreatePullRequest(summary,
                    description,
                    pushToBranchName,
                    currentBranchName,
                    cancellationToken);

                log.Info($"Pull Request [{pullRequest.Title} (#{pullRequest.Number})]({pullRequest.Url}) Created");

                return (pullRequest, vendorApiAdapter.Name);
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Pull Request Creation Failed. Error: {e.Message}", e);
            }
        }

        GitBranchName CalculateBranchName()
        {
            return GitBranchName.CreateFromFriendlyName($"octopus-argo-cd-{Guid.NewGuid().ToString("N").Substring(0, 10)}");
        }

        public void PushChanges(GitBranchName branchName)
        {
            var remote = repository.Network.Remotes.Single();
            repository.Branches.Update(repository.Head,
                                       branch => branch.Remote = remote.Name,
                                       branch => branch.UpstreamBranch = branchName.Value);

            PushStatusError? errorsDetected = null;
            var pushOptions = new PushOptions
            {
                CredentialsProvider = connection.ToLibGit2SharpCredentialHandler(),
                OnPushStatusError = errors => errorsDetected = errors,
                CertificateCheck = connection.ToLibGit2SharpCertificateCheckHandler(log)
            };

            repository.Network.Push(repository.Head, pushOptions);
            if (errorsDetected != null)
            {
                throw new CommandException($"Failed to push to branch {branchName.ToFriendlyName()}. Error: {errorsDetected.Message}");
            }
        }

        void FetchAndRebase(GitBranchName branchName)
        {
            var remote = repository.Network.Remotes.Single();
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification).ToList();
            var fetchOptions = new FetchOptions
            {
                CredentialsProvider = connection.ToLibGit2SharpCredentialHandler(),
                CertificateCheck = connection.ToLibGit2SharpCertificateCheckHandler(log)
            };

            try
            {
                log.Verbose($"Fetching from remote '{remote.Name}'");
                LibGit2Sharp.Commands.Fetch(repository, remote.Name, refSpecs, fetchOptions, null);
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Failed to fetch from remote '{remote.Name}'. Error: {e.Message}", e);
            }

            var trackingBranchName = $"{remote.Name}/{branchName.ToFriendlyName()}";
            var trackingBranch = repository.Branches[trackingBranchName];
            if (trackingBranch == null)
            {
                log.Verbose($"No tracking branch found for '{branchName.ToFriendlyName()}', skipping rebase");
                return;
            }

            log.Verbose($"Rebasing onto '{trackingBranch.FriendlyName}'");
            try
            {
                var rebaseResult = repository.Rebase.Start(null,
                                                           trackingBranch,
                                                           null,
                                                           repositoryIdentity,
                                                           new RebaseOptions());
                if (rebaseResult.Status == RebaseStatus.Conflicts)
                {
                    throw new CommandException($"Rebase conflict detected when rebasing onto '{trackingBranch.FriendlyName}'. Error: Cannot automatically resolve conflicts");
                }

                log.Verbose($"Rebase result: {rebaseResult.Status}");
            }
            catch (LibGit2SharpException e)
            {
                throw new CommandException($"Failed to rebase onto '{trackingBranch.FriendlyName}'. Error: {e.Message}", e);
            }
        }

        static string GenerateCommitMessage(string summary, string description)
        {
            return description.Equals(string.Empty)
                ? summary
                : $"{summary}\n\n{description}";
        }

        public void Dispose()
        {
            //free up the repository handles
            repository?.Dispose();

            //delete the local repository
            log.Verbose("Deleting local repository");
            try
            {
                calamariFileSystem.DeleteDirectory(repoCheckoutDirectoryPath);
                log.Verbose("Deleted local repository");
            }
            catch (Exception e)
            {
                log.VerboseFormat("Failed to delete local repository.{0}{1}", Environment.NewLine, e);
            }
        }
    }

    public record PushResult(string CommitSha, string ShortSha, DateTimeOffset CommitTimestamp);

    public record PullRequestPushResult(
        string CommitSha,
        string ShortSha,
        DateTimeOffset CommitTimestamp,
        string RepositoryUri,
        string PullRequestTitle,
        string PullRequestUri,
        long PullRequestNumber,
        string VendorName) : PushResult(CommitSha, ShortSha, CommitTimestamp);
}

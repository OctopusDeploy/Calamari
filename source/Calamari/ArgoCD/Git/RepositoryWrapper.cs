using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Time;
using LibGit2Sharp;
using Octopus.CoreUtilities.Extensions;
using Polly;
using Polly.Retry;

namespace Calamari.ArgoCD.Git
{
    public class RepositoryWrapper : IDisposable
    {
        readonly Repository repository;
        readonly ICalamariFileSystem calamariFileSystem;
        readonly string repoCheckoutDirectoryPath;
        readonly ILog log;
        readonly IGitConnection connection;
        readonly IGitVendorApiAdapter? vendorApiAdapter;
        readonly IClock clock;

        public string WorkingDirectory => repository.Info.WorkingDirectory;

        public RepositoryWrapper(Repository repository,
                                 ICalamariFileSystem calamariFileSystem,
                                 string repoCheckoutDirectoryPath,
                                 ILog log,
                                 IGitConnection connection,
                                 IGitVendorApiAdapter? vendorApiAdapter,
                                 IClock clock)
        {
            this.repository = repository;
            this.calamariFileSystem = calamariFileSystem;
            this.repoCheckoutDirectoryPath = repoCheckoutDirectoryPath;
            this.log = log;
            this.connection = connection;
            this.vendorApiAdapter = vendorApiAdapter;
            this.clock = clock;
        }

        // returns true if changes were made to the repository
        public bool CommitChanges(string summary, string description)
        {
            try
            {
                var commitTime = clock.GetUtcTime();
                var commitMessage = GenerateCommitMessage(summary, description);
                var commit = repository.Commit(commitMessage,
                                               new Signature("Octopus", "octopus@octopus.com", commitTime),
                                               new Signature("Octopus", "octopus@octopus.com", commitTime));
                log.Verbose($"Committed changes to {commit.ShortSha()}");
                return true;
            }
            catch (EmptyCommitException)
            {
                log.Verbose("No changes required committing.");
                return false;
            }
        }

        public string GetCommitSha()
        {
            return repository.Head.Tip.Sha;
        }

        public void RecursivelyStageFilesForRemoval(string subPath)
        {
            var cleansedSubPath = NormalizePath(subPath);
            if (!cleansedSubPath.EndsWith(Path.DirectorySeparatorChar) && !cleansedSubPath.IsNullOrEmpty())
            {
                cleansedSubPath += Path.DirectorySeparatorChar;
            }

            log.Info("Removing files recursively");
            List<IndexEntry> filesToRemove = repository.Index
                                                       .Where(i => NormalizePath(i.Path).StartsWith(cleansedSubPath))
                                                       .ToList();
            filesToRemove.ForEach(i => repository.Index.Remove(i.Path));
        }

        public void StageFiles(string[] filesToStage)
        {
            foreach (var file in filesToStage)
            {
                repository.Index.Add(NormalizePath(file));
            }
        }

        static string NormalizePath(string path)
        {
            var separatorToReplace = Path.DirectorySeparatorChar == '/' ? '\\' : '/';
            var normalized = path.Replace(separatorToReplace, Path.DirectorySeparatorChar);
            return normalized.StartsWith($".{Path.DirectorySeparatorChar}") ? normalized.Substring(2) : normalized;
        }

        public async Task<PushResult> PushChanges(bool requiresPullRequest,
                                                  string summary,
                                                  string description,
                                                  GitReference branchName,
                                                  CancellationToken cancellationToken)
        {
            var currentBranchName = repository.GetBranchName(branchName);
            var pushToBranchName = requiresPullRequest ? CalculateBranchName() : currentBranchName;

            log.Info($"Pushing changes to branch '{pushToBranchName.ToFriendlyName()}'");

            var retryPipeline = new ResiliencePipelineBuilder()
                                .AddRetry(new RetryStrategyOptions
                                {
                                    ShouldHandle = new PredicateBuilder().Handle<CommandException>().Handle<NonFastForwardException>(),
                                    MaxRetryAttempts = 2,
                                    Delay = TimeSpan.Zero,
                                    OnRetry = args =>
                                    {
                                        log.Verbose($"Push to '{pushToBranchName.ToFriendlyName()}' failed (attempt {args.AttemptNumber}), fetching and merging before retrying");
                                        FetchAndMerge(currentBranchName);
                                        return default;
                                    }
                                })
                                .Build();

            retryPipeline.Execute(() => PushChanges(pushToBranchName));

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
                return new PushResult(commit.Sha, commit.ShortSha());
            }

            var (title, number, uri) = await CreatePullRequest(
                summary,
                description,
                pushToBranchName,
                currentBranchName,
                cancellationToken);

            return new PullRequestPushResult(
                commit.Sha,
                commit.ShortSha(),
                title,
                uri,
                number);
        }

        async Task<PullRequest> CreatePullRequest(
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

                log.SetOutputVariableButDoNotAddToVariables("PullRequest.Title", pullRequest.Title);
                log.SetOutputVariableButDoNotAddToVariables("PullRequest.Number", pullRequest.Number.ToString());
                log.SetOutputVariableButDoNotAddToVariables("PullRequest.Url", pullRequest.Url);

                log.Info($"Pull Request [{pullRequest.Title} (#{pullRequest.Number})]({pullRequest.Url}) Created");

                return pullRequest;
            }
            catch (Exception e)
            {
                throw new CommandException("Pull Request Creation Failed", e);
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
                CredentialsProvider = (url, usernameFromUrl, types) =>
                                          new UsernamePasswordCredentials { Username = connection.Username, Password = connection.Password },
                OnPushStatusError = errors => errorsDetected = errors
            };

            repository.Network.Push(repository.Head, pushOptions);
            if (errorsDetected != null)
            {
                throw new CommandException($"Failed to push to branch {branchName.ToFriendlyName()} - {errorsDetected.Message}");
            }
        }

        void FetchAndMerge(GitBranchName branchName)
        {
            var remote = repository.Network.Remotes.Single();
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification).ToList();
            var fetchOptions = new FetchOptions
            {
                CredentialsProvider = (url, usernameFromUrl, types) =>
                    new UsernamePasswordCredentials { Username = connection.Username, Password = connection.Password }
            };

            log.Verbose($"Fetching from remote '{remote.Name}'");
            LibGit2Sharp.Commands.Fetch(repository, remote.Name, refSpecs, fetchOptions, null);

            var trackingBranchName = $"{remote.Name}/{branchName.ToFriendlyName()}";
            var trackingBranch = repository.Branches[trackingBranchName];
            if (trackingBranch == null)
            {
                log.Verbose($"No tracking branch found for '{branchName.ToFriendlyName()}', skipping merge");
                return;
            }

            log.Verbose($"Merging '{trackingBranch.FriendlyName}' into HEAD");
            var commitTime = clock.GetUtcTime();
            var mergeResult = repository.Merge(trackingBranch,
                                               new Signature("Octopus", "octopus@octopus.com", commitTime),
                                               new MergeOptions { FastForwardStrategy = FastForwardStrategy.Default });

            if (mergeResult.Status == MergeStatus.Conflicts)
            {
                throw new CommandException($"Merge conflict detected when merging '{trackingBranch.FriendlyName}' - cannot automatically resolve conflicts");
            }

            log.Verbose($"Merge result: {mergeResult.Status}");
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
                //some files in the .git folder can/are ReadOnly which makes them impossible to delete
                //so just remove the ReadOnly attribute from all files (if they are ReadOnly)
                foreach (var gitFile in calamariFileSystem.EnumerateFilesRecursively(Path.Combine(repoCheckoutDirectoryPath, ".git")))
                {
                    calamariFileSystem.RemoveReadOnlyAttributeFromFile(gitFile);
                }

                calamariFileSystem.DeleteDirectory(repoCheckoutDirectoryPath);
                log.Verbose("Deleted local repository");
            }
            catch (Exception e)
            {
                log.VerboseFormat("Failed to delete local repository.{0}{1}", Environment.NewLine, e);
            }
        }
    }

    public record PushResult(string CommitSha, string ShortSha);

    public record PullRequestPushResult(
        string CommitSha,
        string ShortSha,
        string PullRequestTitle,
        string PullRequestUri,
        long PullRequestNumber) : PushResult(CommitSha, ShortSha);
}
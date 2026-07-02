using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Git
{
    // A single application source that needs to be written into a Git repository and committed/pushed.
    // The repository URL and target revision (the grouping keys) are derived from the source itself.
    public class RepositorySourceUpdate
    {
        public RepositorySourceUpdate(NamespacedApplicationName applicationName, ApplicationSourceWithMetadata source, ISourceUpdater updater)
        {
            ApplicationName = applicationName;
            Source = source;
            Updater = updater;
        }

        public NamespacedApplicationName ApplicationName { get; }
        public ApplicationSourceWithMetadata Source { get; }
        public ISourceUpdater Updater { get; }

        public string RepoUrl => Source.Source.OriginalRepoUrl;
        public string TargetRevision => Source.Source.TargetRevision;
    }

    // Processes a set of source updates by grouping them so that each repository is cloned once and each
    // branch is checked out once. Within a repo+branch group, changes are committed once per application
    // (direct push) and pushed in a single push; when raising pull requests, behaviour is unchanged - one
    // branch, commit and pull request per source - while still reusing the shared clone and checkout.
    //
    // Failure handling is fail-fast: if an application genuinely fails to commit, the exception propagates,
    // the clone is disposed (so no partial commits are pushed for that group), and the step aborts.
    public class GroupedRepositoryProcessor
    {
        readonly AuthenticatingRepositoryFactory repositoryFactory;
        readonly GitCommitParameters commitParameters;
        readonly ICommitMessageGenerator commitMessageGenerator;

        public GroupedRepositoryProcessor(AuthenticatingRepositoryFactory repositoryFactory,
                                          GitCommitParameters commitParameters,
                                          ICommitMessageGenerator commitMessageGenerator)
        {
            this.repositoryFactory = repositoryFactory;
            this.commitParameters = commitParameters;
            this.commitMessageGenerator = commitMessageGenerator;
        }

        // Returns a result for each update, in the same order the updates were provided.
        public IReadOnlyList<SourceUpdateResult> Process(IReadOnlyList<RepositorySourceUpdate> updates)
        {
            var results = new SourceUpdateResult[updates.Count];
            var indexed = updates.Select((update, index) => (update, index)).ToList();

            foreach (var repoGroup in indexed.GroupBy(x => x.update.RepoUrl))
            {
                var branchGroups = repoGroup.GroupBy(x => x.update.TargetRevision).ToList();

                // Clone once per repository. The clone fetches every branch, so each branch group below just
                // checks out the branch it needs without re-cloning.
                using var repository = repositoryFactory.CloneRepository(repoGroup.Key, branchGroups[0].Key);

                foreach (var branchGroup in branchGroups)
                {
                    var reference = GitReference.CreateFromString(branchGroup.Key);
                    var items = branchGroup.ToList();

                    if (commitParameters.RequiresPr)
                    {
                        ProcessBranchAsPullRequests(repository, reference, items, results);
                    }
                    else
                    {
                        ProcessBranchAsDirectCommits(repository, reference, items, results);
                    }
                }
            }

            return results;
        }

        void ProcessBranchAsDirectCommits(RepositoryWrapper repository,
                                          GitReference reference,
                                          List<(RepositorySourceUpdate update, int index)> items,
                                          SourceUpdateResult[] results)
        {
            repository.CheckoutBranch(reference);

            var committedAnything = false;

            // Apply and commit one application at a time so each commit contains only that application's changes.
            foreach (var applicationGroup in items.GroupBy(x => x.update.ApplicationName.Value))
            {
                // Apply each source and record whether it actually changed the working tree. A source can
                // produce a result without changing anything (e.g. an image already at the target tag), and
                // such a source must not be attributed the application's commit.
                var changedPaths = new HashSet<string>(repository.GetChangedFilePaths());
                var applied = new List<(int index, FileUpdateResult fileResult, bool changedWorkingTree)>();
                foreach (var item in applicationGroup)
                {
                    var fileResult = item.update.Updater.Process(item.update.Source, repository.WorkingDirectory);
                    var changedPathsNow = new HashSet<string>(repository.GetChangedFilePaths());
                    var changedWorkingTree = changedPathsNow.Except(changedPaths).Any();
                    changedPaths = changedPathsNow;
                    applied.Add((item.index, fileResult, changedWorkingTree));
                }

                PushResult? applicationCommit = null;
                if (applied.Any(a => a.changedWorkingTree))
                {
                    repository.StageAllChanges();
                    var description = commitMessageGenerator.GenerateDescription(FileUpdateResult.Merge(applied.Select(a => a.fileResult)));
                    if (repository.CommitChanges(commitParameters.Summary, description))
                    {
                        // Captured before the push. Pushes are serial and the clone is fresh, so a rebase-on-push
                        // (which would rewrite this SHA) is not expected; see RepositoryWrapper.PushChanges.
                        applicationCommit = repository.GetHeadCommitResult();
                        committedAnything = true;
                    }
                }

                foreach (var item in applied)
                {
                    var pushResult = item.changedWorkingTree ? applicationCommit : null;
                    results[item.index] = new SourceUpdateResult(item.fileResult.UpdatedImages, pushResult, item.fileResult.ReplacedFiles, item.fileResult.PatchedFiles);
                }
            }

            // A single push carries every application's commit on this branch.
            if (committedAnything)
            {
                repository.PushChanges(false, commitParameters.Summary, string.Empty, reference, commitParameters.PushRetryAttempts, CancellationToken.None)
                          .GetAwaiter()
                          .GetResult();
            }
        }

        void ProcessBranchAsPullRequests(RepositoryWrapper repository,
                                         GitReference reference,
                                         List<(RepositorySourceUpdate update, int index)> items,
                                         SourceUpdateResult[] results)
        {
            foreach (var item in items)
            {
                // Reset back to the remote tip so each pull request branch contains only this source's commit.
                repository.CheckoutBranch(reference);

                var fileResult = item.update.Updater.Process(item.update.Source, repository.WorkingDirectory);
                if (!fileResult.HasChanges())
                {
                    results[item.index] = new SourceUpdateResult(fileResult.UpdatedImages, null, fileResult.ReplacedFiles, fileResult.PatchedFiles);
                    continue;
                }

                repository.StageAllChanges();
                var description = commitMessageGenerator.GenerateDescription(fileResult);
                if (!repository.CommitChanges(commitParameters.Summary, description))
                {
                    results[item.index] = new SourceUpdateResult(fileResult.UpdatedImages, null, fileResult.ReplacedFiles, fileResult.PatchedFiles);
                    continue;
                }

                var pushResult = repository.PushChanges(true, commitParameters.Summary, description, reference, commitParameters.PushRetryAttempts, CancellationToken.None)
                                           .GetAwaiter()
                                           .GetResult();
                results[item.index] = new SourceUpdateResult(fileResult.UpdatedImages, pushResult, fileResult.ReplacedFiles, fileResult.PatchedFiles);
            }
        }
    }
}

using System;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class RepositoryAdapter
{
    readonly IRepositoryFactory repositoryFactory;
    readonly ILog log;
    readonly ICommitMessageGenerator commitMessageGenerator;
    readonly GitCommitParameters commitParameters;

    public RepositoryAdapter(IRepositoryFactory repositoryFactory,
                             GitCommitParameters commitParameters,
                             ILog log,
                             ICommitMessageGenerator commitMessageGenerator)
    {
        this.repositoryFactory = repositoryFactory;
        this.log = log;
        this.commitMessageGenerator = commitMessageGenerator;
        this.commitParameters = commitParameters;
    }

    // New generic overload — used by CommitToGitConvention and (after Task 3) ArgoCD callers
    public SourceUpdateResult Process(IGitConnection connection, Func<string, FileUpdateResult> updater)
    {
        using var repository = repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), connection);
        var result = updater(repository.WorkingDirectory);
        return PersistChangesToRepository(repository, connection.GitReference, result);
    }

    // Legacy overload — delegates to new one; will be removed in Task 3 after ArgoCD callers are migrated
    public SourceUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, ISourceUpdater updater)
    {
        var connection = new GitConnection(
            null, null,
            GitCloneSafeUrl.FromString(sourceWithMetadata.Source.OriginalRepoUrl),
            GitReference.CreateFromString(sourceWithMetadata.Source.TargetRevision));
        return Process(connection, workingDir => updater.Process(sourceWithMetadata, workingDir));
    }

    SourceUpdateResult PersistChangesToRepository(RepositoryWrapper repository, GitReference gitReference, FileUpdateResult result)
    {
        if (result.HasChanges())
        {
            var pushResult = PushToRemote(repository, gitReference, result);
            if (pushResult is not null)
                return new SourceUpdateResult(result.UpdatedImages, pushResult, result.PatchedFileContent);
        }
        return new SourceUpdateResult([], null, []);
    }

        return new SourceUpdateResult([], null, result.ReplacedFiles, result.PatchedFiles);
    }
    
    
    protected PushResult? PushToRemote(
        RepositoryWrapper repository,
        GitReference branchName, 
        FileUpdateResult result)
    {
        log.Info("Staging files in repository");
        repository.AddFiles(result.ReplacedFiles.Select(f => f.FilePath).Concat(result.PatchedFiles.Select(f => f.FilePath)).Distinct().ToArray());
        repository.RemoveFiles(result.FilesRemoved ?? []);

        var commitDescription = commitMessageGenerator.GenerateDescription(result.UpdatedImages, commitParameters.Description);

        log.Info("Committing changes");
        if (!repository.CommitChanges(commitParameters.Summary, commitDescription))
            return null;

        log.Verbose("Pushing to remote");
        return repository.PushChanges(commitParameters.RequiresPr,
                                      commitParameters.Summary,
                                      commitDescription,
                                      branchName,
                                      CancellationToken.None)
                         .GetAwaiter()
                         .GetResult();
    }
}

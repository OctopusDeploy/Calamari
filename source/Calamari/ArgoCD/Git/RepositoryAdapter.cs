using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class RepositoryAdapter
{
    readonly AuthenticatingRepositoryFactory repositoryFactory;
    readonly ILog log;
    readonly ICommitMessageGenerator commitMessageGenerator;
    readonly GitCommitParameters commitParameters;

    public RepositoryAdapter(AuthenticatingRepositoryFactory repositoryFactory,
                             GitCommitParameters commitParameters,
                             ILog log,
                             ICommitMessageGenerator commitMessageGenerator)
    {
        this.repositoryFactory = repositoryFactory;
        this.log = log;
        this.commitMessageGenerator = commitMessageGenerator;
        this.commitParameters = commitParameters;
    }

    public SourceUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, ISourceUpdater updater)
    {
        using (var repository = repositoryFactory.CloneRepository(sourceWithMetadata.Source.OriginalRepoUrl, sourceWithMetadata.Source.TargetRevision))
        {
            var filesUpdated = updater.Process(sourceWithMetadata, repository.WorkingDirectory);
            return PersistChangesToRepository(repository, sourceWithMetadata.Source.TargetRevision, filesUpdated);
        }
    }
    
    SourceUpdateResult PersistChangesToRepository(RepositoryWrapper repository, string targetRevision, FileUpdateResult result)
    {
        if (result.HasChanges())
        {
            var pushResult = PushToRemote(repository,
                                          GitReference.CreateFromString(targetRevision),
                                          result);

            if (pushResult is not null)
            {
                return new SourceUpdateResult(result.UpdatedImages, pushResult, result.ReplacedFiles, result.PatchedFiles);
            }
        }

        return new SourceUpdateResult([], null, result.ReplacedFiles, result.PatchedFiles);
    }
    
    
    protected PushResult? PushToRemote(
        RepositoryWrapper repository,
        GitReference branchName, 
        FileUpdateResult result)
    {
        log.Info("Staging files in repository");
        repository.RemoveFiles(result.FilesRemoved);
        repository.AddFiles(result.ReplacedFiles.Select(f => f.FilePath).Concat(result.PatchedFiles.Select(f => f.FilePath)).Distinct().ToArray());

        var commitDescription = commitMessageGenerator.GenerateDescription(result);

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
using System;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;

namespace Calamari.ArgoCD.Git;


public class RepositoryAdapter
{
    readonly AuthenticatingRepositoryFactory repositoryFactory;
    readonly RepositoryUpdater  repositoryUpdater;

    public RepositoryAdapter(AuthenticatingRepositoryFactory repositoryFactory,
                             RepositoryUpdater repositoryUpdater)
    {
        this.repositoryFactory = repositoryFactory;
        this.repositoryUpdater = repositoryUpdater;
    }

    public SourceUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, ISourceUpdater updater)
    {
        using var repository = repositoryFactory.CloneRepository(sourceWithMetadata.Source.OriginalRepoUrl, sourceWithMetadata.Source.TargetRevision);
        var filesUpdated = updater.Process(sourceWithMetadata, repository.WorkingDirectory);
        
        if (filesUpdated.HasChanges())
        {
            var pushResult = repositoryUpdater.PushToRemote(repository, GitReference.CreateFromString(sourceWithMetadata.Source.TargetRevision), filesUpdated);
            return new SourceUpdateResult(filesUpdated.UpdatedImages, pushResult, filesUpdated.ReplacedFiles, filesUpdated.PatchedFiles);
        }

        return new SourceUpdateResult([], null, filesUpdated.ReplacedFiles, filesUpdated.PatchedFiles);
    }
}

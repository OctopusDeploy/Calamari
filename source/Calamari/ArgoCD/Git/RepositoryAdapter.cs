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
        if (result.UpdatedImages.Count > 0)
        {
            var pushResult = PushToRemote(repository,
                                          GitReference.CreateFromString(targetRevision),
                                          result);

            if (pushResult is not null)
            {
                return new SourceUpdateResult(result.UpdatedImages, pushResult, result.PatchedFileContent);
            }
        }

        return new SourceUpdateResult([], null, []);
    }
    
    
    protected PushResult? PushToRemote(
        RepositoryWrapper repository,
        GitReference branchName, 
        FileUpdateResult result)
    {
        log.Info("Staging files in repository");
        repository.StageFiles(result.PatchedFileContent.Select(pf => pf.FilePath).Distinct().ToArray());
        repository.UnStageFiles(result.FilesRemoved ?? []);

        var commitDescription = commitMessageGenerator.GenerateDescription(result.UpdatedImages, commitParameters.Description);

        log.Info("Commiting changes");
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
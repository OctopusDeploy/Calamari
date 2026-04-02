using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public async Task<SourceUpdateResult> ProcessAsync(ApplicationSourceWithMetadata sourceWithMetadata, ISourceUpdater updater)
    {
        using (var repository = await repositoryFactory.CloneRepositoryAsync(sourceWithMetadata.Source.OriginalRepoUrl, sourceWithMetadata.Source.TargetRevision))
        {
            var filesUpdated = updater.Process(sourceWithMetadata, repository.WorkingDirectory);
            return await PersistChangesToRepositoryAsync(repository, sourceWithMetadata.Source.TargetRevision, filesUpdated);
        }
    }

    async Task<SourceUpdateResult> PersistChangesToRepositoryAsync(RepositoryWrapper repository, string targetRevision, FileUpdateResult result)
    {
        if (result.HasChanges())
        {
            var pushResult = await PushToRemoteAsync(repository,
                                                     GitReference.CreateFromString(targetRevision),
                                                     result);

            if (pushResult is not null)
            {
                return new SourceUpdateResult(result.UpdatedImages, pushResult, result.ReplacedFiles, result.PatchedFiles);
            }
        }

        return new SourceUpdateResult([], null, result.ReplacedFiles, result.PatchedFiles);
    }


    protected async Task<PushResult?> PushToRemoteAsync(
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
        return await repository.PushChanges(commitParameters.RequiresPr,
                                            commitParameters.Summary,
                                            commitDescription,
                                            branchName,
                                            CancellationToken.None);
    }
}

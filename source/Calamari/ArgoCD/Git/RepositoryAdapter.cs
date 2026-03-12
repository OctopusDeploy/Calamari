using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class RepositoryAdapter
{
    readonly RepositoryFactory repositoryFactory;
    readonly Dictionary<string, GitCredentialDto> gitCredentials;
    readonly ILog log;
    readonly ICommitMessageGenerator commitMessageGenerator;
    readonly ISourceUpdater updater;
    readonly GitCommitParameters commitParameters; 

    public RepositoryAdapter(Dictionary<string, GitCredentialDto> gitCredentials,
                             RepositoryFactory repositoryFactory,
                             GitCommitParameters commitParameters,
                             ILog log,
                             ICommitMessageGenerator commitMessageGenerator,
                             ISourceUpdater updater)
    {
        this.repositoryFactory = repositoryFactory;
        this.gitCredentials = gitCredentials;
        this.log = log;
        this.commitMessageGenerator = commitMessageGenerator;
        this.commitParameters = commitParameters;
        this.updater = updater;
    }

    public SourceUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        using (var repository = CreateRepository(sourceWithMetadata.Source))
        {
            var filesUpdated = updater.Process(sourceWithMetadata, repository.WorkingDirectory);
            return PersistChangesToRepository(repository, sourceWithMetadata, filesUpdated);
        }
    }
    
    protected RepositoryWrapper CreateRepository(ApplicationSource source)
    {
        var gitCredential = gitCredentials.GetValueOrDefault(source.OriginalRepoUrl);
        if (gitCredential == null)
        {
            log.Info($"No Git credentials found for: '{source.OriginalRepoUrl}', will attempt to clone repository anonymously.");
        }

        var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, source.CloneSafeRepoUrl, GitReference.CreateFromString(source.TargetRevision));
        return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
    }
    
    SourceUpdateResult PersistChangesToRepository(RepositoryWrapper repository, ApplicationSourceWithMetadata sourceWithMetadata, FileUpdateResult result)
    {
        if (result.UpdatedImages.Count > 0)
        {
            var pushResult = PushToRemote(repository,
                                          GitReference.CreateFromString(sourceWithMetadata.Source.TargetRevision),
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
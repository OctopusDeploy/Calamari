using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public abstract class BaseUpdater
{
    protected RepositoryFactory repositoryFactory;
    protected Dictionary<string, GitCredentialDto> gitCredentials;
    protected readonly ILog log;
    readonly ICommitMessageGenerator commitMessageGenerator;

    protected BaseUpdater(RepositoryFactory repositoryFactory, Dictionary<string, GitCredentialDto> gitCredentials, ILog log, ICommitMessageGenerator commitMessageGenerator)
    {
        this.repositoryFactory = repositoryFactory;
        this.gitCredentials = gitCredentials;
        this.log = log;
        this.commitMessageGenerator = commitMessageGenerator;
    }

    protected RepositoryWrapper CreateRepository(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        var source = sourceWithMetadata.Source;
        var gitCredential = gitCredentials.GetValueOrDefault(source.OriginalRepoUrl);
        if (gitCredential == null)
        {
            log.Info($"No Git credentials found for: '{source.OriginalRepoUrl}', will attempt to clone repository anonymously.");
        }

        var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, source.CloneSafeRepoUrl, GitReference.CreateFromString(source.TargetRevision));
        return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
    }
    
    PushResult? PushToRemote(
        RepositoryWrapper repository,
        GitReference branchName,
        GitCommitParameters commitParameters,
        HashSet<string> updatedFiles,
        HashSet<string> updatedImages)
    {
        log.Info("Staging files in repository");
        repository.StageFiles(updatedFiles.ToArray());

        var commitDescription = commitMessageGenerator.GenerateDescription(updatedImages, commitParameters.Description);

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
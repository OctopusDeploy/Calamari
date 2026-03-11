using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class RepositoryAdapter
{
    //This might work better if you pass in the repository rather than the factory (so create this above).
    readonly RepositoryFactory repositoryFactory;
    readonly Dictionary<string, GitCredentialDto> gitCredentials;
    readonly ILog log;
    readonly ICommitMessageGenerator commitMessageGenerator;
    readonly ISourceUpdater updater;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig; //only needed for the CommitParameters

    public RepositoryAdapter(Dictionary<string, GitCredentialDto> gitCredentials,
                             RepositoryFactory repositoryFactory,
                             UpdateArgoCDAppDeploymentConfig deploymentConfig,
                             ILog log,
                             ICommitMessageGenerator commitMessageGenerator,    
                             ISourceUpdater updater)
    {
        this.repositoryFactory = repositoryFactory;
        this.gitCredentials = gitCredentials;
        this.log = log;
        this.commitMessageGenerator = commitMessageGenerator;
        this.updater = updater;
        this.deploymentConfig = deploymentConfig;
    }

    public SourceUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        using (var repository = CreateRepository(sourceWithMetadata))
        {
            var filesUpdated = updater.Process(sourceWithMetadata);
            return PersistChangesToRepository(repository, sourceWithMetadata, filesUpdated);
        }
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

        return new SourceUpdateResult(new HashSet<string>(), null, []);
    }
    
    
    protected PushResult? PushToRemote(
        RepositoryWrapper repository,
        GitReference branchName, 
        FileUpdateResult result)
    {
        log.Info("Staging files in repository");
        repository.StageFiles(result.UpdatedFiles.ToArray());

        var commitDescription = commitMessageGenerator.GenerateDescription(result.UpdatedImages, deploymentConfig.CommitParameters.Description);

        log.Info("Commiting changes");
        if (!repository.CommitChanges(deploymentConfig.CommitParameters.Summary, commitDescription))
            return null;

        log.Verbose("Pushing to remote");
        return repository.PushChanges(deploymentConfig.CommitParameters.RequiresPr,
                                      deploymentConfig.CommitParameters.Summary,
                                      commitDescription,
                                      branchName,
                                      CancellationToken.None)
                         .GetAwaiter()
                         .GetResult();
    }
}
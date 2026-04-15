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

    public delegate FileUpdateResult RepositoryMutator(string workingDir);

    // New generic overload — used by CommitToGitConvention and (after Task 3) ArgoCD callers
    public RepositoryUpdates Process(string repoUrl, string targetRevision, RepositoryMutator mutator)
    {
        using var repository = repositoryFactory.CloneRepository(repoUrl, targetRevision);
        var result = mutator(repository.WorkingDirectory);
        return PersistChangesToRepository(repository, targetRevision, result);
    }

    RepositoryUpdates PersistChangesToRepository(RepositoryWrapper repository, string targetRevision, FileUpdateResult result)
    {
        if (result.HasChanges())
        {
            var pushResult = PushToRemote(repository,
                                          GitReference.CreateFromString(targetRevision),
                                          result);

            if (pushResult is not null)
            {
                return new RepositoryUpdates(result.UpdatedImages, pushResult, result.ReplacedFiles, result.PatchedFiles);
            }
        }

        return new RepositoryUpdates([], null, result.ReplacedFiles, result.PatchedFiles);
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

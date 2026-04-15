using System;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;


public class RepositoryAdapter
{
    readonly AuthenticatingRepositoryFactory repositoryFactory;
    readonly RepositoryUpdater  repositoryUpdater;
    readonly ICommitMessageGenerator commitMessageGenerator;

    public RepositoryAdapter(AuthenticatingRepositoryFactory repositoryFactory,
                             ICommitMessageGenerator commitMessageGenerator,
                             RepositoryUpdater repositoryUpdater)
    {
        this.repositoryFactory = repositoryFactory;
        this.commitMessageGenerator = commitMessageGenerator;
        this.repositoryUpdater = repositoryUpdater;
    }

    public delegate FileUpdateResult RepositoryMutator(string workingDir);

    // New generic overload — used by CommitToGitConvention and (after Task 3) ArgoCD callers
    public RepositoryUpdates Process(string repoUrl, string targetRevision, RepositoryMutator mutator)
    {
        using var repository = repositoryFactory.CloneRepository(repoUrl, targetRevision);
        var result = mutator(repository.WorkingDirectory);
        
        if (result.HasChanges())
        {
            var pushResult = repositoryUpdater.PushToRemote(repository, GitReference.CreateFromString(targetRevision), result);
            return new RepositoryUpdates(result.UpdatedImages, pushResult, result.ReplacedFiles, result.PatchedFiles);
        }

        return new RepositoryUpdates([], null, result.ReplacedFiles, result.PatchedFiles);
    }
}

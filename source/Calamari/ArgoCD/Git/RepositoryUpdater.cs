using System.Threading;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class RepositoryUpdater
{
    readonly ILog log;
    readonly GitCommitParameters commitParameters;
    readonly ICommitMessageGenerator commitMessageGenerator;

    public RepositoryUpdater(GitCommitParameters commitParameters, ILog log, ICommitMessageGenerator commitMessageGenerator)
    {
        this.commitParameters = commitParameters;
        this.log = log;
        this.commitMessageGenerator = commitMessageGenerator;
    }
    
    public PushResult? PushToRemote(
        RepositoryWrapper repository,
        GitReference branchName,
        FileUpdateResult result)
    {
        log.Info("Staging files in repository");
        repository.StageAllChanges();
        
        var changeDescription = commitMessageGenerator.GenerateDescription(result);
        
        log.Info("Committing changes");
        if (!repository.CommitChanges(commitParameters.Summary, changeDescription))
            return null;

        log.Verbose("Pushing to remote");
        return repository.PushChanges(commitParameters.RequiresPr,
                                      commitParameters.Summary,
                                      changeDescription,
                                      branchName,
                                      CancellationToken.None)
                         .GetAwaiter()
                         .GetResult();
    }
}
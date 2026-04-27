using System.Collections.Generic;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Git;

public class AuthenticatingRepositoryFactory
{
    readonly Dictionary<string, GitCredentialDto> gitCredentials;
    readonly RepositoryFactory repositoryFactory;
    readonly ILog log;
    

    public AuthenticatingRepositoryFactory(Dictionary<string, GitCredentialDto> gitCredentials, RepositoryFactory repositoryFactory, ILog log)
    {
        this.gitCredentials = gitCredentials;
        this.repositoryFactory = repositoryFactory;
        this.log = log;
    }
    
    public RepositoryWrapper CloneRepository(string requestedUrl, string targetRevision)
    {
        var gitCredential = gitCredentials.GetValueOrDefault(requestedUrl);
        if (gitCredential == null)
        {
            log.Info($"No Git credentials found for: '{requestedUrl}', will attempt to clone repository anonymously.");
        }

        var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, GitCloneSafeUrl.FromString(requestedUrl), GitReference.CreateFromString(targetRevision));
        return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
    }
}

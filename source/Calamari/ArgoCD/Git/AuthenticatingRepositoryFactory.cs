using System.Collections.Generic;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Git;

public class AuthenticatingRepositoryFactory
{
    readonly Dictionary<string, IGitCredentialDto> gitCredentials;
    readonly IRepositoryFactory repositoryFactory;
    readonly ILog log;

    public AuthenticatingRepositoryFactory(
        Dictionary<string, IGitCredentialDto> gitCredentials,
        IRepositoryFactory repositoryFactory,
        ILog log)
    {
        this.gitCredentials = gitCredentials;
        this.repositoryFactory = repositoryFactory;
        this.log = log;
    }

    public RepositoryWrapper CloneRepository(string requestedUrl, string targetRevision)
    {
        var gitCredential = gitCredentials.GetValueOrDefault(requestedUrl);
        if (gitCredential is GitCredentialDto passwordCredential)
        {
            var gitConnection = new HttpsGitConnection(passwordCredential.Username, passwordCredential.Password, GitCloneSafeUrl.ConvertToUriString(requestedUrl), GitReference.CreateFromString(targetRevision));
            return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
        }

        log.Info($"No Git credentials found for: '{requestedUrl}', will attempt to clone repository anonymously.");
        var anonGitConnection = new HttpsGitConnection(null, null, GitCloneSafeUrl.ConvertToUriString(requestedUrl), GitReference.CreateFromString(targetRevision));
        return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), anonGitConnection);
    }
}
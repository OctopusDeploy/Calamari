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
        switch (gitCredential)
        {
            case GitCredentialDto passwordCredential:
            {
                var gitConnection = new HttpsGitConnection(passwordCredential.Username, passwordCredential.Password, GitCloneSafeUrl.ConvertToUriString(requestedUrl), GitReference.CreateFromString(targetRevision));
                return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
            }
            case SshKeyGitCredentialDto sshCredential:
            {
                var sshConnection = new SshGitConnection(
                    sshCredential.Username,
                    requestedUrl,
                    GitReference.CreateFromString(targetRevision),
                    sshCredential.PrivateKey);
                return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), sshConnection);
            }
        }

        log.Info($"No Git credentials found for: '{requestedUrl}', will attempt to clone repository anonymously.");
        // SCP-style URLs (git@github.com:org/repo.git) are rewritten to HTTPS by GitCloneSafeUrl.
        // Anonymous HTTPS clone may fail with 401/404, which is confusing for SSH-only repos.
        var anonGitConnection = new HttpsGitConnection(null, null, GitCloneSafeUrl.ConvertToUriString(requestedUrl), GitReference.CreateFromString(targetRevision));
        return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), anonGitConnection);
    }
}
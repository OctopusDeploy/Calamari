using System.Collections.Generic;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Git;

public class AuthenticatingRepositoryFactory
{
    readonly Dictionary<string, GitCredentialDto> gitCredentials;
    readonly Dictionary<string, GitCredentialSshKeyDto> sshCredentials;
    readonly RepositoryFactory repositoryFactory;
    readonly ILog log;

    public AuthenticatingRepositoryFactory(
        Dictionary<string, GitCredentialDto> gitCredentials,
        Dictionary<string, GitCredentialSshKeyDto> sshCredentials,
        RepositoryFactory repositoryFactory,
        ILog log)
    {
        this.gitCredentials = gitCredentials;
        this.sshCredentials = sshCredentials;
        this.repositoryFactory = repositoryFactory;
        this.log = log;
    }

    public RepositoryWrapper CloneRepository(string requestedUrl, string targetRevision)
    {
        var sshCredential = sshCredentials.GetValueOrDefault(requestedUrl);
        if (sshCredential is not null)
        {
            var sshConnection = new SshGitConnection(
                sshCredential.Username,
                requestedUrl,
                GitReference.CreateFromString(targetRevision),
                sshCredential.PrivateKey,
                sshCredential.PublicKey,
                sshCredential.Passphrase);
            return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), sshConnection);
        }

        var gitCredential = gitCredentials.GetValueOrDefault(requestedUrl);
        if (gitCredential == null)
        {
            log.Info($"No Git credentials found for: '{requestedUrl}', will attempt to clone repository anonymously.");
        }

        var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, GitCloneSafeUrl.FromString(requestedUrl), GitReference.CreateFromString(targetRevision));
        return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
    }
}
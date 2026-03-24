using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Dtos;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class AuthenticatingRepositoryFactory
{
    readonly Dictionary<GitRepositoryAddress, GitCredentialDto> gitCredentials;
    readonly Dictionary<GitRepositoryAddress, SshGitCredentialDto> sshGitCredentials;
    readonly RepositoryFactory repositoryFactory;
    readonly ILog log;

    public AuthenticatingRepositoryFactory(Dictionary<string, GitCredentialDto> gitCredentials, Dictionary<string, SshGitCredentialDto> sshGitCredentials, RepositoryFactory repositoryFactory, ILog log)
    {
        this.gitCredentials = gitCredentials.ToDictionary(kv => new GitRepositoryAddress(kv.Key), kv => kv.Value);
        this.sshGitCredentials = sshGitCredentials.ToDictionary(kv => new GitRepositoryAddress(kv.Key), kv => kv.Value);
        this.repositoryFactory = repositoryFactory;
        this.log = log;
    }

    public RepositoryWrapper CloneRepository(GitRepositoryAddress requestedAddress, string targetRevision)
    {
        var gitReference = GitReference.CreateFromString(targetRevision);

        var sshCredential = sshGitCredentials.GetValueOrDefault(requestedAddress);
        if (sshCredential != null)
        {
            var sshConnection = new SshGitConnection(sshCredential.Username, sshCredential.PrivateKey, sshCredential.PublicKey, sshCredential.Passphrase, requestedAddress.Normalized, gitReference);
            return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), sshConnection);
        }

        var gitCredential = gitCredentials.GetValueOrDefault(requestedAddress);
        if (gitCredential == null)
        {
            log.Info($"No Git credentials found for: '{requestedAddress}', will attempt to clone repository anonymously.");
        }

        var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, requestedAddress.Normalized, gitReference);
        return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
    }
}
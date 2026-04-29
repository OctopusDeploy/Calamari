using System.Collections.Generic;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Git;

public class AuthenticatingRepositoryFactory
{
    readonly Dictionary<string, GitCredentialDto> httpsGitCredentials;
    readonly Dictionary<string, GitCredentialSshKeyDto> sshGitCredentials;
    readonly IRepositoryFactory repositoryFactory;
    readonly ILog log;

    public AuthenticatingRepositoryFactory(
        Dictionary<string, GitCredentialDto> httpsGitCredentials,
        Dictionary<string, GitCredentialSshKeyDto> sshGitCredentials,
        IRepositoryFactory repositoryFactory,
        ILog log)
    {
        this.httpsGitCredentials = httpsGitCredentials;
        this.sshGitCredentials = sshGitCredentials;
        this.repositoryFactory = repositoryFactory;
        this.log = log;
    }

    public RepositoryWrapper CloneRepository(string requestedUrl, string targetRevision)
    {
        var httpsGitCredential = httpsGitCredentials.GetValueOrDefault(requestedUrl);
        if (httpsGitCredential is not null)
        {
            var gitConnection = new HttpsGitConnection(httpsGitCredential.Username, httpsGitCredential.Password, GitCloneSafeUrl.FromString(requestedUrl), GitReference.CreateFromString(targetRevision));
            return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
        }

        var sshGitCredential = sshGitCredentials.GetValueOrDefault(requestedUrl);
        if (sshGitCredential is not null)
        {
            var sshConnection = new SshGitConnection(
                sshGitCredential.Username,
                requestedUrl,
                GitReference.CreateFromString(targetRevision),
                sshGitCredential.PrivateKey,
                sshGitCredential.PublicKey,
                sshGitCredential.Passphrase);
            return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), sshConnection);
        }

        log.Info($"No Git credentials found for: '{requestedUrl}', will attempt to clone repository anonymously.");
        // SCP-style URLs (git@github.com:org/repo.git) are rewritten to HTTPS by GitCloneSafeUrl.
        // Anonymous HTTPS clone may fail with 401/404, which is confusing for SSH-only repos.
        var anonGitConnection = new HttpsGitConnection(null, null, GitCloneSafeUrl.FromString(requestedUrl), GitReference.CreateFromString(targetRevision));
        return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), anonGitConnection);
    }
}
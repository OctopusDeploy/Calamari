using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Git;

public class AuthenticatingRepositoryFactory
{
    readonly Dictionary<string, IGitCredentialDto> gitCredentials;
    readonly IRepositoryFactory repositoryFactory;
    readonly ILog log;

    public AuthenticatingRepositoryFactory(
        IReadOnlyCollection<IGitCredentialDto> gitCredentials,
        IRepositoryFactory repositoryFactory,
        ILog log)
    {
        // Takes the first git credential per URL, with a preference for username/password credentials (they are more broadly useful as they can be used for PR creation)
        this.gitCredentials = gitCredentials
                              .GroupBy(c => c.Url)
                              .ToDictionary(g => g.Key, g => g.OfType<GitCredentialDto>().FirstOrDefault<IGitCredentialDto>() ?? g.First());

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
                var sshConnection = new SshKeyGitConnection(
                    sshCredential.Username,
                    sshCredential.PrivateKey,
                    requestedUrl,
                    GitReference.CreateFromString(targetRevision),
                    sshCredential.KnownHosts.Select(kh => new SshKnownHost(kh.Host, kh.PublicKey)).ToArray());
                return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), sshConnection);
            }
            case null:
            {
                log.Info($"No Git credentials found for: '{requestedUrl}', will attempt to clone repository anonymously.");
                break;
            }
            default:
            {
                log.Warn($"An unrecognised credential type '{gitCredential.GetType().Name}' was found for '{requestedUrl}'. Ignoring the credentials and attempting an anonymous clone.");
                break;
            }
        }

        var anonGitConnection = new HttpsGitConnection(null, null, GitCloneSafeUrl.ConvertToUriString(requestedUrl), GitReference.CreateFromString(targetRevision));
        return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), anonGitConnection);
    }
}

#nullable enable

using System;
using System.Linq;
using Octopus.Calamari.Contracts.Git;
using ArgoCdIGitCredentialDto = Octopus.Calamari.Contracts.ArgoCD.IGitCredentialDto;
using ArgoCdGitCredentialDto = Octopus.Calamari.Contracts.ArgoCD.GitCredentialDto;
using ArgoCdSshKeyGitCredentialDto = Octopus.Calamari.Contracts.ArgoCD.SshKeyGitCredentialDto;

namespace Calamari.ArgoCD.Git;

public static class GitConnectionFactory
{
    // Converts Argo credentials into the common type
    // ideally we migrate the Argo code path to use the common type from server and we can delete this
    public static IGitConnection Create(ArgoCdIGitCredentialDto? credential, string url, GitReference gitReference)
    {
        return credential switch
        {
            ArgoCdGitCredentialDto password => Create(
                new UsernamePasswordGitCredentialDto(string.Empty, password.Url, password.Username, password.Password),
                url,
                gitReference
            ),
            ArgoCdSshKeyGitCredentialDto ssh => Create(
                new SshKeyGitCredentialDto(
                    string.Empty,
                    ssh.Url,
                    ssh.Username,
                    ssh.PrivateKey,
                    ssh.KnownHosts.Select(kh => new SshKnownHostDto(kh.Host, kh.PublicKey)).ToArray()),
                url,
                gitReference
            ),
            _ => Create((IGitCredentialDto?)null, url, gitReference),
        };
    }

    public static IGitConnection Create(IGitCredentialDto? credential, string url, GitReference gitReference)
    {
        return credential switch
        {
            UsernamePasswordGitCredentialDto password
                => new UsernamePasswordGitConnection(password.Username, password.Password, url, gitReference),
            SshKeyGitCredentialDto ssh
                => new SshKeyGitConnection(
                    ssh.Username,
                    ssh.PrivateKey,
                    url,
                    gitReference,
                    ssh.KnownHosts.Select(kh => new SshKnownHost(kh.Host, kh.PublicKey)).ToArray()
                ),
            null => new AnonymousGitConnection(url, gitReference),
            _ => throw new NotSupportedException($"An unrecognised credential type '{credential.GetType().Name}' was found for '{url}'")
        };
    }
}
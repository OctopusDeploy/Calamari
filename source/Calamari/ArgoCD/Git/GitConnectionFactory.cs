#nullable enable

using System;
using System.Linq;
using Calamari.Common.Commands;
using Octopus.Calamari.Contracts.Git;
using ArgoCdIGitCredentialDto = Octopus.Calamari.Contracts.ArgoCD.IGitCredentialDto;
using ArgoCdGitCredentialDto = Octopus.Calamari.Contracts.ArgoCD.GitCredentialDto;
using ArgoCdSshKeyGitCredentialDto = Octopus.Calamari.Contracts.ArgoCD.SshKeyGitCredentialDto;

namespace Calamari.ArgoCD.Git;

public static class GitConnectionFactory
{
    // Converts Argo credentials into the common type
    // ideally we migrate the Argo code path to use the common type from server and we can delete this
    public static IGitConnection Create(ArgoCdIGitCredentialDto? credential, string url, GitReference gitReference, bool createsPr)
    {
        return credential switch
        {
            ArgoCdGitCredentialDto password => Create(
                new UsernamePasswordGitCredentialDto(string.Empty, password.Url, password.Username, password.Password),
                url,
                gitReference,
                createsPr),
            ArgoCdSshKeyGitCredentialDto ssh => Create(
                new SshKeyGitCredentialDto(
                    string.Empty,
                    ssh.Url,
                    ssh.Username,
                    ssh.PrivateKey,
                    ssh.KnownHosts.Select(kh => new SshKnownHostDto(kh.Host, kh.PublicKey)).ToArray()),
                url,
                gitReference,
                createsPr),
            _ => Create((IGitCredentialDto?)null, url, gitReference, createsPr),
        };
    }

    public static IGitConnection Create(IGitCredentialDto? credential, string url, GitReference gitReference, bool createsPr)
    {
        return credential switch
        {
            UsernamePasswordGitCredentialDto password
                => new UsernamePasswordGitConnection(password.Username, password.Password, url, gitReference),

            SshKeyGitCredentialDto when createsPr
                => throw new CommandException("Creating PRs is not possible when using SSH key authentication, please use a username and password instead"),

            SshKeyGitCredentialDto ssh
                => new SshKeyGitConnection(
                    ssh.Username,
                    ssh.PrivateKey,
                    url,
                    gitReference,
                    ssh.KnownHosts.Select(kh => new SshKnownHost(kh.Host, kh.PublicKey)).ToArray()),

            null when createsPr
                => throw new CommandException("Creating a pull request requires Git repository credentials, but none were provided. Please configure a username and password."),

            null => new AnonymousGitConnection(url, gitReference),

            _ => throw new NotSupportedException($"An unrecognised credential type '{credential.GetType().Name}' was found for '{url}'")
        };
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
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

    public RepositoryWrapper CloneRepository(string requestedUrl, string targetRevision, bool requiresPullRequest)
    {
        var gitCredential = gitCredentials.GetValueOrDefault(requestedUrl);
        var gitConnection = GitConnectionFactory.Create(gitCredential, requestedUrl, GitReference.CreateFromString(targetRevision));

        if (requiresPullRequest)
        {
            switch (gitConnection)
            {
                case SshKeyGitConnection:
                    throw new CommandException("Creating PRs is not possible when using SSH key authentication, please use a username and password instead");
                case AnonymousGitConnection:
                    throw new CommandException("Creating a pull request requires Git repository credentials, but none were provided. Please configure a username and password.");
            }
        }

        if (gitConnection is AnonymousGitConnection)
        {
            log.Info($"No Git credentials found for: '{requestedUrl}', will attempt to clone repository anonymously.");
        }

        return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Git;

public class AuthenticatingRepositoryFactory
{
    readonly Dictionary<string, IGitCredentialDto> credentialsByUrl;
    readonly IRepositoryFactory repositoryFactory;
    readonly ILog log;

    public AuthenticatingRepositoryFactory(
        IReadOnlyCollection<IGitCredentialDto> gitCredentials,
        IRepositoryFactory repositoryFactory,
        ILog log)
    {
        // When more than one credential is supplied for the same URL we pick one based on the URL style:
        // SSH/SCP URLs prefer the SSH key credential; everything else prefers a username/password credential.
        credentialsByUrl = gitCredentials.GroupBy(c => c.Url).ToDictionary(g => g.Key, SelectCredentialForUrl);

        this.repositoryFactory = repositoryFactory;
        this.log = log;
    }

    public RepositoryWrapper CloneRepository(string requestedUrl, string targetRevision, bool requiresPullRequest)
    {
        var credential = credentialsByUrl.GetValueOrDefault(requestedUrl);
        var gitReference = GitReference.CreateFromString(targetRevision);

        if (requiresPullRequest && credential is not GitCredentialDto)
        {
            throw new CommandException(
                $"Pull request creation is enabled but no username/password credential is available for '{requestedUrl}'. "
                + "Supply a username/password credential for this repository so the vendor API can be reached.");
        }

        var gitConnection = BuildGitConnection(credential, requestedUrl, gitReference);

        if (!requiresPullRequest)
        {
            return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
        }

        return repositoryFactory.CloneRepositoryWithPullRequestClient(UniqueRepoNameGenerator.Generate(), gitConnection);
    }

    IGitConnection BuildGitConnection(IGitCredentialDto? credential, string requestedUrl, GitReference gitReference)
    {
        if (credential is null)
        {
            log.Info($"No Git credentials found for: '{requestedUrl}', will attempt to clone repository anonymously.");
            return new HttpsGitConnection(null, null, GitCloneSafeUrl.ConvertToUriString(requestedUrl), gitReference);
        }

        return credential switch
        {
            GitCredentialDto up => new HttpsGitConnection(
                up.Username,
                up.Password,
                GitCloneSafeUrl.ConvertToUriString(requestedUrl),
                gitReference),
            SshKeyGitCredentialDto ssh => new SshKeyGitConnection(
                ssh.Username,
                ssh.PrivateKey,
                requestedUrl,
                gitReference,
                ssh.KnownHosts.Select(kh => new SshKnownHost(kh.Host, kh.PublicKey)).ToArray()),
            _ => throw new NotSupportedException($"An unrecognised credential type '{credential.GetType().Name}' was found for '{requestedUrl}'"),
        };
    }

    static IGitCredentialDto SelectCredentialForUrl(IGrouping<string, IGitCredentialDto> group)
    {
        if (IsSshOrScpUrl(group.Key))
        {
            return group.OfType<SshKeyGitCredentialDto>().FirstOrDefault() ?? group.First();
        }

        return group.OfType<GitCredentialDto>().FirstOrDefault() ?? group.First();
    }

    // Matches the SCP-style git URL form `user@host:path` (any username, not just `git`).
    // The character classes exclude `/` so URLs containing `://` (e.g. ssh://, https://) won't match.
    static readonly Regex ScpStyleUrlRegex = new(@"^[^/@\s]+@[^/:\s]+:", RegexOptions.Compiled);

    static bool IsSshOrScpUrl(string url) =>
        url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)
        || ScpStyleUrlRegex.IsMatch(url);
}
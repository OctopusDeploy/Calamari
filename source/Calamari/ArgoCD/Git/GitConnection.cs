#nullable enable

using System;
using System.Collections.Generic;
using Calamari.Common.Commands;

namespace Calamari.ArgoCD.Git
{
    public interface IRepositoryConnection
    {
        public string Url { get; }
    }

    public interface IGitConnection : IRepositoryConnection
    {
        public GitReference GitReference { get; }
    }

    public interface IHttpsGitConnection : IGitConnection
    {
        string? Username { get; }
        string? Password { get; }

        // Resolved as lazy because we don't have a strong trust that the input data is of the correct format
        // If this is _not_ a URI, the existing code would throw an error when it gets used - so we want to
        // replicate that same lazy error throwing.
        public Lazy<Uri> Uri { get; }
    }

    public class HttpsGitConnection : IHttpsGitConnection
    {
        public HttpsGitConnection(string? username, string? password, string url, GitReference gitReference)
        {
            Username = username;
            Password = password;
            Url = url;
            GitReference = gitReference;
            Uri = new Lazy<Uri>(() => ParseAsHttpsUri(Url));
        }

        public string? Username { get; }
        public string? Password { get; }
        public string Url { get; }
        public GitReference GitReference { get; }

        public Lazy<Uri> Uri { get; }

        static Uri ParseAsHttpsUri(string repositoryUrl)
        {
            if (!System.Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri))
            {
                throw new CommandException(
                    $"Pull request operations require an HTTPS repository URL, but got: '{repositoryUrl}'. "
                    + "SCP-style SSH URLs (e.g. git@github.com:org/repo.git) are not supported for pull request creation.");
            }

            return uri;
        }
    }

    public class AnonymousGitConnection(string url, GitReference gitReference)
        : HttpsGitConnection(null, null, url, gitReference);

    public class UsernamePasswordGitConnection(string username, string password, string url, GitReference gitReference)
        : HttpsGitConnection(username, password, url, gitReference);

    public record SshKeyGitConnection(
        string? Username,
        string PrivateKey,
        string Url,
        GitReference GitReference,
        IReadOnlyList<SshKnownHost> KnownHosts)
        : IGitConnection;

    public record SshKnownHost(string Host, string PublicKey);
}
#nullable enable

using System;
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

    public class SshKeyGitConnection : IGitConnection
    {
        public SshKeyGitConnection(
            string? username,
            string privateKey,
            string url,
            GitReference gitReference,
            SshKnownHost[] knownHosts)
        {
            Username = username;
            PrivateKey = privateKey;
            Url = url;
            GitReference = gitReference;
            KnownHosts = knownHosts;
        }

        public string? Username { get; }
        public string PrivateKey { get; }
        public string Url { get; }
        public GitReference GitReference { get; }
        public SshKnownHost[] KnownHosts { get; }
    }

    public record SshKnownHost(string Host, string PublicKey);
}
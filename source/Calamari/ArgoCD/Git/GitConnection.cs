#nullable enable

using System;

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
                throw new InvalidOperationException(
                    $"Pull request operations require an HTTPS repository URL, but got: '{repositoryUrl}'. "
                    + "SCP-style SSH URLs (e.g. git@github.com:org/repo.git) are not supported for pull request creation.");
            }

            return uri;
        }
    }
}
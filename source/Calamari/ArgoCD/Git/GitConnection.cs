#nullable enable

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
    }

    public class HttpsGitConnection : IHttpsGitConnection
    {
        public HttpsGitConnection(string? username, string? password, string url, GitReference gitReference)
        {
            Username = username;
            Password = password;
            Url = url;
            GitReference = gitReference;
        }

        public string? Username { get; }
        public string? Password { get; }
        public string Url { get; }
        public GitReference GitReference { get; }
    }
}
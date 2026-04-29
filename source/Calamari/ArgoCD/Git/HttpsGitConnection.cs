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

    public class SshGitConnection : IGitConnection
    {
        public SshGitConnection(
            string? username,
            string url,
            GitReference gitReference,
            string privateKey,
            string publicKey,
            string? passphrase)
        {
            Username = username;
            Url = url;
            GitReference = gitReference;
            PrivateKey = privateKey;
            PublicKey = publicKey;
            Passphrase = passphrase;
        }

        public string? Username { get; }
        public string? Password => null;
        public string Url { get; }
        public GitReference GitReference { get; }
        public string PrivateKey { get; }
        public string PublicKey { get; }
        public string? Passphrase { get; }
    }
}
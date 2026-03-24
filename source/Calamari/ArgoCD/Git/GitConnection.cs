#nullable enable
using System;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Git
{
    public interface IRepositoryConnection
    {
        public string? Username { get;  }
        public string? Password { get;  }
        public Uri Url { get;  }
    }

    public interface IGitConnection : IRepositoryConnection
    {
        public GitReference GitReference { get;  }

        // TODO: This ain't it
        public bool IsSsh => false;
        public string? PrivateKey => null;
        public string? PublicKey => null;
        public string? Passphrase => null;

        public Credentials CreateCredentials()
        {
            if (IsSsh && PrivateKey != null)
            {
                return new SshUserKeyMemoryCredentials
                {
                    Username = Username ?? "git",
                    PrivateKey = PrivateKey!,
                    PublicKey = PublicKey ?? "",
                    Passphrase = Passphrase ?? ""
                };
            }

            return new UsernamePasswordCredentials
            {
                Username = Username,
                Password = Password
            };
        }
    }

    public class GitConnection : IGitConnection
    {
        public GitConnection(string? username, string? password, Uri url, GitReference gitReference)
        {
            Username = username;
            Password = password;
            Url = url;
            GitReference = gitReference;
        }

        public string? Username { get; }
        public string? Password { get; }
        public Uri Url { get; }
        public GitReference GitReference { get; }
    }

    public class SshGitConnection : IGitConnection
    {
        public SshGitConnection(string username, string privateKey, string publicKey, string passphrase, Uri url, GitReference gitReference)
        {
            Username = username;
            PrivateKey = privateKey;
            PublicKey = publicKey;
            Passphrase = passphrase;
            Url = url;
            GitReference = gitReference;
        }

        public string Username { get; }
        public string? Password => null;
        public Uri Url { get; }
        public GitReference GitReference { get; }
        public bool IsSsh => true;
        public string PrivateKey { get; }
        public string PublicKey { get; }
        public string Passphrase { get; }
    }
}
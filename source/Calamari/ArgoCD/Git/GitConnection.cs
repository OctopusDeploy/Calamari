#nullable enable
using System;

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
}
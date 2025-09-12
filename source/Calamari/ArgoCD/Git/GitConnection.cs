#nullable enable
using System;

namespace Calamari.ArgoCD.Git
{
    public interface IRepositoryConnection
    {
        public string? Username { get;  }
        public string? Password { get;  }
        public string Url { get;  }
    }
    
    public interface IGitConnection : IRepositoryConnection
    {
        public GitBranchName BranchName { get;  }
    }

    public class GitConnection : IGitConnection
    {
        public GitConnection(string? username, string? password, string url, GitBranchName branchName)
        {
            Username = username;
            Password = password;
            Url = url;
            BranchName = branchName;
        }

        public string? Username { get; }
        public string? Password { get; }
        public string Url { get; }
        public GitBranchName BranchName { get; }
    }
}
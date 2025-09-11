#nullable enable
using System;
using Calamari.ArgoCD.Dtos;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;

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
        
        public static GitConnection Create(ArgoCDApplicationSourceDto source, GitCredentialDto gitCredentials)
        {
            return new GitConnection(gitCredentials.Username, gitCredentials.Password, source.Url, new GitBranchName(source.TargetRevision));
        }
    }
}
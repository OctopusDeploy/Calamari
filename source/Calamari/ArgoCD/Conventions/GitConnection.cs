#nullable enable
using System;

namespace Calamari.ArgoCD.Conventions
{

    public class GitRepository
    {
        public GitRepository(string url, string? username, string? password)
        {
            Url = url;
            Username = username;
            Password = password;
        }
        
        public string Url { get; }
        public string? Username { get; }
        public string? Password { get; }
    }
    public class RepositoryBranchFolder
    {
        public RepositoryBranchFolder(GitRepository repository, string branchName, string folder)
        {
            Repository = repository;
            BranchName = branchName;
            Folder = folder;
        }

        public GitRepository Repository { get; }
        public string BranchName { get; }
        public string Folder { get; }
        public string RemoteBranchName => $"origin/{BranchName}";
    }
}
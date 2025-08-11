#nullable enable
using System;

namespace Calamari.ArgoCD.Commands.Executors
{
    class GitConnection
    {
        public GitConnection(string url, string branchName, string? username, string? password, string folder)
        {
            Url = url;
            BranchName = branchName;
            Username = username;
            Password = password;
            Folder = folder;
        }

        public string Url { get; }
        public string BranchName { get; }
        public string? Username { get; }
        public string? Password { get; }
        public string Folder { get; }
        public string RemoteBranchName => $"origin/{BranchName}";
    }
}
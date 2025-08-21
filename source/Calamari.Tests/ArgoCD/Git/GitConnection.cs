using System;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;

namespace Calamari.Tests.ArgoCD.Git
{
    public class GitConnection : IGitConnection
    {
        public GitConnection(string username,
                             string password,
                             string url,
                             string branchName)
        {
            Username = username;
            Password = password;
            Url = url;
            BranchName = branchName;
        }

        public string Username { get; }
        public string Password { get; }
        public string Url { get; }
        public string BranchName { get; }
        public string RemoteBranchName => $"origin/{BranchName}";
    }
}
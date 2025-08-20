using Calamari.ArgoCD.Conventions;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    public class GitConnection : IGitConnection
    {
        public GitConnection(string username,
                             string password,
                             string url,
                             string branchName,
                             string subFolder)
        {
            Username = username;
            Password = password;
            Url = url;
            BranchName = branchName;
            SubFolder = subFolder;
        }

        public string Username { get; }
        public string Password { get; }
        public string Url { get; }
        public string BranchName { get; }
        public string SubFolder { get; }
        public string RemoteBranchName => $"origin/{BranchName}";
    }
}
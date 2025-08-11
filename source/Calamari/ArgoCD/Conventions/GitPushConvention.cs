using System;
using Calamari.Common.Commands;
using Calamari.Deployment.Conventions;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Conventions
{
    public class GitPushConvention : IInstallConvention
    {
        readonly GitInstallationContext context;

        public GitPushConvention(GitInstallationContext context)
        {
            this.context = context;
        }

        public void Install(RunningDeployment deployment)
        {
            foreach (var repository in context.Repositories)
            {
                PushChanges("blah", repository);
            }
        }

        void PushChanges(string branchName, Repository repo)
        {
            repo.Commit("Updated the git repo",
                        new Signature("Octopus", "octopus@octopus.com", DateTimeOffset.Now),
                        new Signature("Octopus", "octopus@octopus.com", DateTimeOffset.Now));
            
            Remote remote = repo.Network.Remotes["origin"];
            repo.Branches.Update(repo.Head, 
                                 branch => branch.Remote = remote.Name,
                                 branch => branch.UpstreamBranch = $"refs/heads/{branchName}");
            
            repo.Network.Push(repo.Head);
        }
    }
}
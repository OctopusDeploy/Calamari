using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Commands.Executors;
using Calamari.Common.Commands;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Conventions
{
    public class GitCloneConvention : IInstallConvention
    {
        readonly GitInstallationContext gitInstallContext;
        const string GitRepositoryDirectoryName = "git";

        public GitCloneConvention(GitInstallationContext gitInstallContext)
        {
            this.gitInstallContext = gitInstallContext;
        }

        public void Install(RunningDeployment deployment)
        {
            // NOTE: This could be MULTIPLE REPOSITORIES
            var url = deployment.Variables.Get(SpecialVariables.Git.Url)!;
            var branchName = deployment.Variables.Get(SpecialVariables.Git.BranchName);
            var username = deployment.Variables.Get(SpecialVariables.Git.Username);
            var password = deployment.Variables.Get(SpecialVariables.Git.Password);
            var folder = deployment.Variables.Get(SpecialVariables.Git.Folder) ?? "";

            var gitConnection = new RepositoryBranchFolder(
                                                           new GitRepository(url,
                                                           username,
                                                           password),
                                                           branchName!,
                                                           folder!);

            var repo = CloneRepository(gitConnection, deployment.CurrentDirectory);
            gitInstallContext.AddRepository(repo);
        }

        Repository CloneRepository(RepositoryBranchFolder gitConnection, string rootDir)
        {
            var repositoryPath = Path.Combine(rootDir, GitRepositoryDirectoryName);
            Directory.CreateDirectory(repositoryPath);
            return CheckoutGitRepository(gitConnection, repositoryPath);
        }

        Repository CheckoutGitRepository(RepositoryBranchFolder gitConnection, string checkoutPath)
        {
            //Todo - cannot make this work
            // var options = new CloneOptions
            // {
            //     BranchName = gitConnection.BranchName
            // };

            var options = new CloneOptions();
            if (gitConnection.Repository.Username != null && gitConnection.Repository.Password != null)
            {
                options.FetchOptions = new FetchOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) => new UsernamePasswordCredentials
                    {
                        Username = gitConnection.Repository.Username!,
                        Password = gitConnection.Repository.Password!
                    }
                };
            }

            var repoPath = Repository.Clone(gitConnection.Repository.Url, checkoutPath, options);
            var repo = new Repository(repoPath);
            Branch remoteBranch = repo.Branches[gitConnection.RemoteBranchName];

            //A local branch is required such that libgit2sharp can create "tracking" data
            // libgit2sharp does not support pushing from a detached head
            repo.CreateBranch(gitConnection.BranchName, remoteBranch.Tip);
            LibGit2Sharp.Commands.Checkout(repo, gitConnection.BranchName);
            return repo;
        }
    }
}
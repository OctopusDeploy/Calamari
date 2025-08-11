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
        string repositorySubPath = "repo";
        public List<Repository> Repositories { get; private set; }

        public GitCloneConvention(string repositorySubPath)
        {
            this.repositorySubPath = repositorySubPath;
            Repositories = new List<Repository>();
        }

        public void Install(RunningDeployment deployment)
        {
            var url = deployment.Variables.Get(SpecialVariables.Git.Url);
            var branchName = deployment.Variables.Get(SpecialVariables.Git.BranchName);
            var username = deployment.Variables.Get(SpecialVariables.Git.Username);
            var password = deployment.Variables.Get(SpecialVariables.Git.Password);
            var folder = deployment.Variables.Get(SpecialVariables.Git.Folder);

            var gitConnection = new GitConnection(url!,
                                                  branchName!,
                                                  username,
                                                  password,
                                                  folder!);

            Repositories.Add(CloneRepository(gitConnection, Path.Combine(deployment.CurrentDirectory, repositorySubPath)));
        }

        Repository CloneRepository(GitConnection gitConnection, string rootDir)
        {
            var repositoryPath = Path.Combine(rootDir, repositorySubPath);
            Directory.CreateDirectory(repositoryPath);
            return CheckoutGitRepository(gitConnection, repositoryPath);
        }

        Repository CheckoutGitRepository(GitConnection gitConnection, string checkoutPath)
        {
            //Todo - cannot make this work
            // var options = new CloneOptions
            // {
            //     BranchName = gitConnection.BranchName
            // };

            var options = new CloneOptions();
            if (gitConnection.Username != null && gitConnection.Password != null)
            {
                options.FetchOptions = new FetchOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) => new UsernamePasswordCredentials
                    {
                        Username = gitConnection.Username!,
                        Password = gitConnection.Password!
                    }
                };
            }

            var repoPath = Repository.Clone(gitConnection.Url, checkoutPath, options);
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
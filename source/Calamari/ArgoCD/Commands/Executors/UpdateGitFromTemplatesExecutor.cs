#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes;
using LibGit2Sharp;
using Repository = LibGit2Sharp.Repository;

namespace Calamari.ArgoCD.Commands.Executors
{
    class FileToCopy
    {
        public FileToCopy(string absolutePath, string relativePath)
        {
            RelativePath = relativePath;
            AbsolutePath = absolutePath;
        }
        public string AbsolutePath { get; }
        public string RelativePath { get;  }
    }
    
    class StepFields
    {
        public StepFields(GitConnection gitConnection, List<string> fileGlobs)
        {
            GitConnection = gitConnection;
            FileGlobs = fileGlobs;
        }

        public GitConnection GitConnection { get; }
        public List<string> FileGlobs { get; }
    }

    public class UpdateGitFromTemplatesExecutor
    {
        readonly string repoPath = "git";
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public UpdateGitFromTemplatesExecutor(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }
        
        public async Task<bool> Execute(RunningDeployment deployment, string extractedPackageDirectory)
        {
            await Task.CompletedTask;
            var url = deployment.Variables.Get(SpecialVariables.Git.Url);
            var branchName = deployment.Variables.Get(SpecialVariables.Git.BranchName);
            var username = deployment.Variables.Get(SpecialVariables.Git.Username);
            var password = deployment.Variables.Get(SpecialVariables.Git.Password);
            var folder = deployment.Variables.Get(SpecialVariables.Git.Folder);

            var stepFields = new StepFields(
                new GitConnection(url!, branchName!, username, password, folder!),
                deployment.Variables.GetPaths(SpecialVariables.CustomResourceYamlFileName)
            );
            
            log.Info($"Executing ArgoCD");
            var filesToApply = SelectFiles(extractedPackageDirectory, stepFields.FileGlobs);
            
            var localRepository = CloneRepository(stepFields.GitConnection, deployment.CurrentDirectory);
            
            ApplyChangesToLocalRepository(filesToApply, localRepository, folder!);
             
            PushChanges(stepFields.GitConnection.BranchName, localRepository);
            return true;
        }

        Repository CloneRepository(GitConnection gitConnection, string rootDir)
        {
            var repositoryPath = Path.Combine(rootDir, repoPath);
            Directory.CreateDirectory(repositoryPath);
            return CheckoutGitRepository(gitConnection, repositoryPath);            
        }

        void ApplyChangesToLocalRepository(IEnumerable<FileToCopy> filesToCopy, Repository repository, string repoSubFolder)
        {
            foreach (var file in filesToCopy)
            {
                var repoRelativeFilePath = Path.Combine(repoSubFolder, file.RelativePath);
                var absRepoFilePath = Path.Combine(repository.Info.WorkingDirectory, repoRelativeFilePath);
                EnsureParentDirectoryExists(absRepoFilePath);
                File.Copy(file.AbsolutePath, absRepoFilePath, true);
                
                //This MUST take a path relative to the repository root.
                repository.Index.Add(repoRelativeFilePath);
            }
        }

        void EnsureParentDirectoryExists(string filePath)
        {
            var destinationDirectory = Path.GetDirectoryName(filePath);
            if (destinationDirectory != null)
            {
                Directory.CreateDirectory(destinationDirectory);
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

        IEnumerable<FileToCopy> SelectFiles(string pathToExtractedPackage, List<string> fileGlobs)
        {
            return fileGlobs.SelectMany(glob => fileSystem.EnumerateFilesWithGlob(pathToExtractedPackage, glob))
                            .Select(absoluteFilepath =>
                                    {
                                        #if NETCORE
                                        var relativePath = Path.GetRelativePath(pathToExtractedPackage, file);
                                        #else           
                                        var relativePath = absoluteFilepath.Substring(pathToExtractedPackage.Length + 1);
                                        #endif
                                        return new FileToCopy(absoluteFilepath, relativePath);
                                    });
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
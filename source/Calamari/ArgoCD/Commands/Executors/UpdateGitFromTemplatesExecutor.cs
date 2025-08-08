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
            UpdateRepository(filesToApply, deployment.CurrentDirectory, stepFields);
             
            return true;
        }


        void UpdateRepository(IEnumerable<FileToCopy> filesToCopy, string rootDir, StepFields stepFields)
        {
            var repository = Path.Combine(rootDir, repoPath);
            Directory.CreateDirectory(repository);
            var repo = CheckoutGitRepository(stepFields.GitConnection, repository);
            
            foreach (var file in filesToCopy)
            {
                CopyFileIntoRepository(stepFields.GitConnection.Folder, file, repository, repo);
            }

            PushChanges(stepFields.GitConnection.BranchName, repo);
        }

        static void CopyFileIntoRepository(string gitSubFolder, FileToCopy file, string repositoryPath, Repository repo)
        {
            //The file destination will be the same path wrt package
            var repositoryRelativePath = Path.Combine(gitSubFolder, file.RelativePath);
            var destinationPath = Path.Combine(repositoryPath, repositoryRelativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (destinationDirectory != null)
            {
                Directory.CreateDirectory(destinationDirectory);
            }
            File.Copy(file.AbsolutePath, Path.Combine(repositoryPath, repositoryRelativePath), true);
            repo.Index.Add(repositoryRelativePath);
        }

        static void PushChanges(string branchName, Repository repo)
        {
            Branch localBranch = repo.Branches[branchName];
            repo.Commit("Updated the git repo",
                        new Signature("Octopus", "octopus@octopus.com", DateTimeOffset.Now),
                        new Signature("Octopus", "octopus@octopus.com", DateTimeOffset.Now));
            
            Remote remote = repo.Network.Remotes["origin"];
            repo.Branches.Update(localBranch, 
                                 branch => branch.Remote = remote.Name,
                                 branch => branch.UpstreamBranch = localBranch.CanonicalName);
            
            repo.Network.Push(localBranch);
        }

        IEnumerable<FileToCopy> SelectFiles(string pathToExtractedPackage, List<string> fileGlobs)
        {
            return fileGlobs.SelectMany(glob => fileSystem.EnumerateFilesWithGlob(pathToExtractedPackage, glob))
                            .Select(file =>
                                    {
                                        #if NETCORE
                                        var relativePath = Path.GetRelativePath(pathToExtractedPackage, file);
                                        #else           
                                        var relativePath = file.Substring(pathToExtractedPackage.Length + 1);
                                        #endif
                                        return new FileToCopy(file, relativePath);
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
            var branch = repo.CreateBranch(gitConnection.BranchName, remoteBranch.Tip);
            LibGit2Sharp.Commands.Checkout(repo, gitConnection.BranchName);
            return repo;
        }
    }
}
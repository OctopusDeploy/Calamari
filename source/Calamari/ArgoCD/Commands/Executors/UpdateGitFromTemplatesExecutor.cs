using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Util;
using LibGit2Sharp;
using Repository = LibGit2Sharp.Repository;

namespace Calamari.ArgoCD.Commands.Executors
{
    class GitConnection
    {
        public GitConnection(string url, string branchName, string username, string password, string folder)
        {
            Url = url;
            BranchName = branchName;
            Username = username;
            Password = password;
            Folder = folder;
        }

        public string Url { get; }
        public string BranchName { get; }
        public string Username { get; }
        public string Password { get; }
        public string Folder { get; }
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
        
        public async Task<bool> Execute(RunningDeployment deployment, string inputDirectory)
        {
            await Task.CompletedTask;
            var url = deployment.Variables.Get(SpecialVariables.Git.Url);
            var branchName = deployment.Variables.Get(SpecialVariables.Git.BranchName);
            var username = deployment.Variables.Get(SpecialVariables.Git.Username);
            var password = deployment.Variables.Get(SpecialVariables.Git.Password);
            var folder = deployment.Variables.Get(SpecialVariables.Git.Folder);

            var stepFields = new StepFields(
                new GitConnection(url, branchName, username, password, folder),
                deployment.Variables.GetPaths(SpecialVariables.CustomResourceYamlFileName)
            );
            
            DoWork(stepFields, deployment, inputDirectory);
             
            return true;
        }
        
        void DoWork(StepFields stepFields, RunningDeployment deployment, string inputDirectory)
        {
            //Does a deployment get its own directory every time? If so - this will work for now, if not, this is kinda messy
            log.Info($"Executing ArgoCD");
            
            var filesToApply = SelectFiles(inputDirectory, stepFields.FileGlobs);
            UpdateRepository(filesToApply, deployment.CurrentDirectory, stepFields);
        }

        void UpdateRepository(IEnumerable<RelativeGlobMatch> filesToApply, string rootDir, StepFields stepFields)
        {
            var repository = Path.Combine(rootDir, repoPath);
            var repo = CheckoutGitRepository(stepFields.GitConnection, repository);
            
            foreach (var file in filesToApply)
            {
                //The file destination will be the same path wrt package
                File.Copy(file.FilePath, Path.Combine(repository, stepFields.GitConnection.Folder, file.MappedRelativePath), true);
                repo.Index.Add(file.MappedRelativePath);
            }

            Branch localBranch = repo.Branches[stepFields.GitConnection.BranchName];
            
            repo.Commit("Updated the git repo",
                        new Signature("Octopus", "octopus@octopus.com", DateTimeOffset.Now),
                        new Signature("Octopus", "octopus@octopus.com", DateTimeOffset.Now));
            
            Remote remote = repo.Network.Remotes["origin"];
            repo.Branches.Update(localBranch, 
                                 branch => branch.Remote = remote.Name,
                                 branch => branch.UpstreamBranch = localBranch.CanonicalName);
            
            repo.Network.Push(localBranch);
            
        }

        IEnumerable<RelativeGlobMatch> SelectFiles(string pathToExtractedPackage, List<string> fileGlobs)
        {
            var relativeGlobber = new RelativeGlobber((@base, pattern) => fileSystem.EnumerateFilesWithGlob(@base, pattern), pathToExtractedPackage);
            return fileGlobs.SelectMany(glob => relativeGlobber.EnumerateFilesWithGlob(glob)).ToList();
        }

        Repository CheckoutGitRepository(GitConnection gitConnection, string checkoutPath)
        {
            var options = new CloneOptions
            {
                BranchName = gitConnection.BranchName,
                FetchOptions = new FetchOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) => new UsernamePasswordCredentials
                    {
                        Username = gitConnection.Username,
                        Password = gitConnection.Password
                    }
                }
            };
            Repository.Clone(gitConnection.Url, checkoutPath, options);
            return new Repository(checkoutPath);
        }
    }
}
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
        public GitConnection(string url, string branchName, string username, string password)
        {
            Url = url;
            BranchName = branchName;
            Username = username;
            Password = password;
        }

        public string Url { get; }
        public string BranchName { get; }
        public string Username { get; }
        public string Password { get; }
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

    public class GitTemplateExecutor
    {
        readonly string gitRepoPath = "git";
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public GitTemplateExecutor(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }
        
        public async Task<bool> Execute(RunningDeployment deployment, string inputDirectory)
        {
            await Task.CompletedTask;
            var gitURL = deployment.Variables.Get("Octopus.Action.ArgoCD.Git.URL");
            var branchName = deployment.Variables.Get("Octopus.Action.ArgoCD.Git.BranchName");
            var username = deployment.Variables.Get("Octopus.Action.ArgoCD.Git.Username");
            var password = deployment.Variables.Get("Octopus.Action.ArgoCD.Git.Password");

            var stepFields = new StepFields(
                new GitConnection(gitURL, branchName, username, password),
                //TODO(tmm): Does frontend separate based on \n, or \r\n?
                deployment.Variables.GetPaths(SpecialVariables.CustomResourceYamlFileName)
            );
            
            DoWork(stepFields, deployment, inputDirectory);
             
            return true;
        }
        
        void DoWork(StepFields stepFields, RunningDeployment deployment, string inputDirectory)
        {
            //Does a deployment get its own directory every time? If so - this will work for now, if not, this is kinda messy
            log.Info($"Executing ArgoCD {gitRepoPath}");
            
            var filesToApply = SelectFiles(inputDirectory, stepFields.FileGlobs);
            
            var repositoryPath = Path.Combine(deployment.CurrentDirectory, gitRepoPath);
            var repo = CheckoutGitRepository(stepFields.GitConnection, repositoryPath);
            
            foreach (var file in filesToApply)
            {
                //The file destination will be the same path wrt package
                File.Copy(file.FilePath, Path.Combine(repositoryPath, file.MappedRelativePath), true);
                repo.Index.Add(file.MappedRelativePath);
            }
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
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes;
using Repository = LibGit2Sharp.Repository;

namespace Calamari.ArgoCD.Conventions
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
        public StepFields(RepositoryBranchFolder gitConnection, List<string> fileGlobs)
        {
            GitConnection = gitConnection;
            FileGlobs = fileGlobs;
        }

        public RepositoryBranchFolder GitConnection { get; }
        public List<string> FileGlobs { get; }
    }

    public class UpdateGitRepositoryInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly string repositoryParentDirectory;
        readonly ILog log;

        public UpdateGitRepositoryInstallConvention(ICalamariFileSystem fileSystem, string repositoryParentDirectory, ILog log)
        {
            this.fileSystem = fileSystem;
            this.repositoryParentDirectory = repositoryParentDirectory;
            this.log = log;
        }
        
        public void Install(RunningDeployment deployment)
        {
            var url = deployment.Variables.Get(SpecialVariables.Git.Url)!;
            var branchName = deployment.Variables.Get(SpecialVariables.Git.BranchName)!;
            var username = deployment.Variables.Get(SpecialVariables.Git.Username);
            var password = deployment.Variables.Get(SpecialVariables.Git.Password);
            var folder = deployment.Variables.Get(SpecialVariables.Git.Folder) ?? string.Empty;

            var stepFields = new StepFields(
                                            new RepositoryBranchFolder(new GitRepository(url, username, password), branchName, folder),
                                            deployment.Variables.GetPaths(SpecialVariables.CustomResourceYamlFileName)
                                           );
            
            log.Info($"Executing ArgoCD");
            var filesToApply = SelectFiles(deployment.CurrentDirectory, stepFields.FileGlobs);
            
            var localRepository = RepositoryHelpers.CloneRepository(repositoryParentDirectory, stepFields.GitConnection);
            
            var filesAdded = CopyFilesIntoPlace(filesToApply, localRepository.Info.WorkingDirectory, folder);
            
            RepositoryHelpers.StageFiles(filesAdded, localRepository);
             
            RepositoryHelpers.PushChanges(stepFields.GitConnection.BranchName, localRepository);
        }

        List<string> CopyFilesIntoPlace(IEnumerable<FileToCopy> filesToCopy, string destinationRootDir, string repoSubFolder)
        {
            var repoRelativeFiles = new List<string>();
            foreach (var file in filesToCopy)
            {
                var repoRelativeFilePath = Path.Combine(repoSubFolder, file.RelativePath);
                var absRepoFilePath = Path.Combine(destinationRootDir, repoRelativeFilePath);
                EnsureParentDirectoryExists(absRepoFilePath);
                File.Copy(file.AbsolutePath, absRepoFilePath, true);
                
                //This MUST take a path relative to the repository root.
                repoRelativeFiles.Add(repoRelativeFilePath);
            }

            return repoRelativeFiles;
        }

        void EnsureParentDirectoryExists(string filePath)
        {
            var destinationDirectory = Path.GetDirectoryName(filePath);
            if (destinationDirectory != null)
            {
                Directory.CreateDirectory(destinationDirectory);
            }
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
    }
}
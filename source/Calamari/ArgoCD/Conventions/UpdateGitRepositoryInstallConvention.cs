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
    public class FileToCopy
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
            var repositoryIndexes = deployment.Variables.GetIndexes(SpecialVariables.Git.Index);

            log.Info($"Executing ArgoCD");
            var fileGlob = deployment.Variables.GetPaths(SpecialVariables.CustomResourceYamlFileName);
            var filesToApply = SelectFiles(deployment.CurrentDirectory, fileGlob).ToList();
            
            foreach (var repositoryName in repositoryIndexes)
            {
                IGitConnection gitConnection = new VariableBackedGitConnection(deployment.Variables, repositoryName);
                UpdateRepository(gitConnection, filesToApply);
            }
        }

        void UpdateRepository(IGitConnection gitConnection, List<FileToCopy> filesToApply)
        {
            var localRepository = RepositoryHelpers.CloneRepository(repositoryParentDirectory, gitConnection);
            var filesAdded = CopyFilesIntoPlace(filesToApply, localRepository.Info.WorkingDirectory, gitConnection.SubFolder);
            RepositoryHelpers.StageFiles(filesAdded, localRepository);
            RepositoryHelpers.PushChanges(gitConnection.BranchName, localRepository);
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
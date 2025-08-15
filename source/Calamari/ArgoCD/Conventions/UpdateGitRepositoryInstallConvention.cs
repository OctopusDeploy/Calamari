#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes;
using LibGit2Sharp;

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

            Log.Info("Executing Commit To Git operation");
            var fileGlob = deployment.Variables.GetPaths(SpecialVariables.CustomResourceYamlFileName);
            var filesToApply = SelectFiles(deployment.CurrentDirectory, fileGlob).ToList();
            
            var commitMessage = GenerateCommitMessage(deployment);
            var requiresPullRequest = FeatureToggle.ArgocdCreatePullRequestFeatureToggle.IsEnabled(deployment.Variables) && deployment.Variables.Get(SpecialVariables.Git.CommitMethod) == "PullRequest";
            
            log.Info($"Found {filesToApply.Count} files to apply");
            
            var repositoryFactory = new RepositoryFactory(log, repositoryParentDirectory);
            foreach (var repositoryIndex in repositoryIndexes)
            {
                Log.Info($"Writing files to repository for index {repositoryIndex}");
                IGitConnection gitConnection = new VariableBackedGitConnection(deployment.Variables, repositoryIndex);

                var repository = repositoryFactory.CloneRepository(repositoryIndex, gitConnection);
                Log.Info("Copying files into repository");
                var filesAdded = CopyFilesIntoPlace(filesToApply, repository.WorkingDirectory, gitConnection.SubFolder);
                Log.Info("Staging files in repository");
                repository.StageFiles(filesAdded);
                Log.Info("Commiting changes");
                repository.CommitChanges(commitMessage);
                
                repository.PushChanges(requiresPullRequest, gitConnection.BranchName);
            }
        }

        string GenerateCommitMessage(RunningDeployment deployment)
        {
            var summary = deployment.Variables.GetMandatoryVariable(SpecialVariables.Git.CommitMessageSummary);
            var description = deployment.Variables.Get(SpecialVariables.Git.CommitMessageDescription) ?? string.Empty;
            return description.Equals(string.Empty)
                ? summary
                : $"{summary}\n\n{description}";
        }


        List<string> CopyFilesIntoPlace(IEnumerable<FileToCopy> filesToCopy, string destinationRootDir, string repoSubFolder)
        {
            var repoRelativeFiles = new List<string>();
            foreach (var file in filesToCopy)
            {
                var repoRelativeFilePath = Path.Combine(repoSubFolder, file.RelativePath);
                var absRepoFilePath = Path.Combine(destinationRootDir, repoRelativeFilePath);
                Log.VerboseFormat($"Copying '{file.AbsolutePath}' to '{absRepoFilePath}'");
                EnsureParentDirectoryExists(absRepoFilePath);
                File.Copy(file.AbsolutePath, absRepoFilePath, true);
                
                repoRelativeFiles.Add(repoRelativeFilePath); //This MUST take a path relative to the repository root.
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
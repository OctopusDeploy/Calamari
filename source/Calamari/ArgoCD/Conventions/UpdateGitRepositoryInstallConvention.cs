#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateGitRepositoryInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly string packageSubfolder;
        readonly IGitHubPullRequestCreator pullRequestCreator;

        public UpdateGitRepositoryInstallConvention(ICalamariFileSystem fileSystem, string packageSubfolder, ILog log, IGitHubPullRequestCreator pullRequestCreator)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            this.pullRequestCreator = pullRequestCreator;
            this.packageSubfolder = packageSubfolder;
        }
        
        public void Install(RunningDeployment deployment)
        {
            Log.Info("Executing Commit To Git operation");
            var packageFiles = GetReferencedPackageFiles(deployment);

            var requiresPullRequest = RequiresPullRequest(deployment);
            var summary = deployment.Variables.GetMandatoryVariable(SpecialVariables.Git.CommitMessageSummary);
            var description = deployment.Variables.Get(SpecialVariables.Git.CommitMessageDescription) ?? string.Empty;
            
            var repositoryFactory = new RepositoryFactory(log, deployment.CurrentDirectory, pullRequestCreator);
            var repositoryIndexes = deployment.Variables.GetIndexes(SpecialVariables.Git.Index);
            log.Info($"Found the following repository indicies '{repositoryIndexes.Join(",")}'");
            foreach (var repositoryIndex in repositoryIndexes)
            {
                Log.Info($"Writing files to repository for '{repositoryIndex}'");
                IGitConnection gitConnection = new VariableBackedGitConnection(deployment.Variables, repositoryIndex);
                var subFolder = deployment.Variables.Get(SpecialVariables.Git.SubFolder(repositoryIndex), String.Empty) ?? String.Empty;
                var purgeOutput = deployment.Variables.GetFlag(SpecialVariables.Git.PurgeOutput);

                var repository = repositoryFactory.CloneRepository(repositoryIndex, gitConnection);
                
                Log.Info($"Copying files into repository {gitConnection.Url}");
                if (purgeOutput)
                {
                    var recursive = deployment.Variables.GetFlag(SpecialVariables.Git.Recursive, false);
                    repository.StageFilesForRemoval(subFolder, recursive);
                }

                var repositoryFiles = packageFiles.Select(f => new FileCopySpecification(f, repository.WorkingDirectory, subFolder)).ToList();
                Log.VerboseFormat("Copying files into subfolder '{0}'", subFolder);
                CopyFiles(repositoryFiles);
                
                Log.Info("Staging files in repository");
                repository.StageFiles(repositoryFiles.Select(fcs => fcs.DestinationRelativePath).ToArray());
                
                Log.Info("Commiting changes");
                if (repository.CommitChanges(summary, description))
                {
                    Log.Info("Changes were commited, pushing to remote");
                    repository.PushChanges(requiresPullRequest, gitConnection.BranchName, CancellationToken.None).GetAwaiter().GetResult();    
                }
                else
                {
                    Log.Info("No changes were commited.");
                }
            }
        }

        bool RequiresPullRequest(RunningDeployment deployment)
        {
            return OctopusFeatureToggles.ArgoCDCreatePullRequestFeatureToggle.IsEnabled(deployment.Variables) && deployment.Variables.Get(SpecialVariables.Git.CommitMethod) == SpecialVariables.Git.GitCommitMethods.PullRequest;

        }

        void CopyFiles(IEnumerable<IFileCopySpecification> repositoryFiles)
        {
            foreach (var file in repositoryFiles)
            {
                Log.VerboseFormat($"Copying '{file.SourceAbsolutePath}' to '{file.DestinationAbsolutePath}'");
                EnsureParentDirectoryExists(file.DestinationAbsolutePath);
                fileSystem.CopyFile(file.SourceAbsolutePath, file.DestinationAbsolutePath);
            }
        }
        
        static void EnsureParentDirectoryExists(string filePath)
        {
            var destinationDirectory = Path.GetDirectoryName(filePath);
            if (destinationDirectory != null)
            {
                Directory.CreateDirectory(destinationDirectory);    
            }
        }

        IPackageRelativeFile[] GetReferencedPackageFiles(RunningDeployment deployment)
        {
            var inputPath = deployment.Variables.GetMandatoryVariable(SpecialVariables.Git.InputPath);
            var recursive = deployment.Variables.GetFlag(SpecialVariables.Git.Recursive, false);
            log.Info($"Selecting files from package using '{inputPath}' (recursive: {recursive})");
            var filesToApply = SelectFiles(Path.Combine(deployment.CurrentDirectory, packageSubfolder), inputPath, recursive);
            log.Info($"Found {filesToApply.Length} files to apply");
            return filesToApply;
        }
        
        IPackageRelativeFile[] SelectFiles(string pathToExtractedPackageFiles, string inputPath, bool recursive)
        {
            var absInputPath = Path.Combine(pathToExtractedPackageFiles, inputPath);
            if (File.Exists(absInputPath))
            {
                //No, this is probably wrong - it _probably_ needs to go into the absolute _basePath_
                return new[] { new PackageRelativeFile(absInputPath, Path.GetFileName(absInputPath)) };
            }
            
            if (Directory.Exists(absInputPath))
            {
                //we really only do yaml files.
                var yamlFileSearchPattern = new[] {"*.yaml", "*.yml"};
                IEnumerable<string> fileList;
                if (recursive)
                {
                    fileList = fileSystem.EnumerateFilesRecursively(absInputPath, yamlFileSearchPattern);
                }
                else
                {
                    fileList = fileSystem.EnumerateFiles(absInputPath, yamlFileSearchPattern);
                }
                
                return fileList.Select(absoluteFilepath =>
                                       {
#if NET
                                           var relativePath = Path.GetRelativePath(absInputPath, absoluteFilepath);
#else
                                                  var relativePath = absoluteFilepath.Substring(absInputPath.Length + 1);
#endif
                                           return new PackageRelativeFile(absoluteFilepath, relativePath);
                                       })
                               .ToArray<IPackageRelativeFile>();
            }
            throw new InvalidOperationException($"Supplied input path '{inputPath}' does not exist within the supplied package");
        }
    }
}

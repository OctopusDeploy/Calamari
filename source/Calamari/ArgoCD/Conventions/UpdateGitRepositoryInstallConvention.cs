#if NET
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateGitRepositoryInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly string packageSubfolder;
        readonly IGitHubPullRequestCreator pullRequestCreator;
        readonly ArgoCommitToGitConfigFactory argoCommitToGitConfigFactory;

        public UpdateGitRepositoryInstallConvention(ICalamariFileSystem fileSystem, string packageSubfolder, ILog log, IGitHubPullRequestCreator pullRequestCreator,
                                                    ArgoCommitToGitConfigFactory argoCommitToGitConfigFactory)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            this.pullRequestCreator = pullRequestCreator;
            this.argoCommitToGitConfigFactory = argoCommitToGitConfigFactory;
            this.packageSubfolder = packageSubfolder;
        }
        
        public void Install(RunningDeployment deployment)
        {
            Log.Info("Executing Commit To Git operation");
            var actionConfig = argoCommitToGitConfigFactory.Create(deployment);
            var packageFiles = GetReferencedPackageFiles(actionConfig);
            
            var repositoryFactory = new RepositoryFactory(log, actionConfig.WorkingDirectory, pullRequestCreator);

            var repositoryIndex = 0;
            foreach (var argoSource in actionConfig.ArgoSourcesToUpdate)
            {
                var repoName = repositoryIndex.ToString();
                Log.Info($"Writing files to git repository for '{argoSource.Url}'");
                var repository = repositoryFactory.CloneRepository(repoName, argoSource);

                var repositoryFiles = packageFiles.Select(f => new FileCopySpecification(f, repository.WorkingDirectory, argoSource.SubFolder)).ToList();
                Log.VerboseFormat("Copying files into subfolder '{0}'", argoSource.SubFolder);
                CopyFiles(repositoryFiles);
                
                Log.Info("Staging files in repository");
                repository.StageFiles(repositoryFiles.Select(fcs => fcs.DestinationRelativePath).ToArray());
                
                Log.Info("Commiting changes");
                if (repository.CommitChanges(actionConfig.CommitSummary, actionConfig.CommitDescription))
                {
                    Log.Info("Changes were commited, pushing to remote");
                    repository.PushChanges(actionConfig.RequiresPr, argoSource.BranchName, CancellationToken.None).GetAwaiter().GetResult();    
                }
                else
                {
                    Log.Info("No changes were commited.");
                }

                repositoryIndex++;
            }
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
        
         IPackageRelativeFile[] GetReferencedPackageFiles(ArgoCommitToGitConfig config)
        {
            log.Info($"Selecting files from package using '{config.InputSubPath}' (recursive: {config.RecurseInputPath})");
            var filesToApply = SelectFiles(Path.Combine(config.WorkingDirectory, packageSubfolder), config);
            log.Info($"Found {filesToApply.Length} files to apply");
            return filesToApply;
        }
        
        IPackageRelativeFile[] SelectFiles(string pathToExtractedPackageFiles, ArgoCommitToGitConfig config)
        {
            var absInputPath = Path.Combine(pathToExtractedPackageFiles, config.InputSubPath);
            if (File.Exists(absInputPath))
            {
                return new[] { new PackageRelativeFile(absolutePath: absInputPath, packageRelativePath: Path.GetFileName(absInputPath)) };
            }
            
            if (Directory.Exists(absInputPath))
            {
                IEnumerable<string> fileList;
                if (config.RecurseInputPath)
                {
                    fileList = fileSystem.EnumerateFilesRecursively(absInputPath, config.FileGlobs);
                }
                else
                {
                    fileList = fileSystem.EnumerateFiles(absInputPath, config.FileGlobs);
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
            throw new InvalidOperationException($"Supplied input path '{config.InputSubPath}' does not exist within the supplied package");
        }
    }
}
#endif
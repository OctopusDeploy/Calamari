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
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes;
using Microsoft.Azure.Management.Compute.Fluent.Models;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateGitRepositoryInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly string packageSubfolder;
        readonly IGitHubPullRequestCreator pullRequestCreator;
        readonly ICustomPropertiesFactory customPropertiesFactory;

        public UpdateGitRepositoryInstallConvention(ICalamariFileSystem fileSystem, string packageSubfolder, ILog log, IGitHubPullRequestCreator pullRequestCreator, ICustomPropertiesFactory customPropertiesFactory)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            this.pullRequestCreator = pullRequestCreator;
            this.customPropertiesFactory = customPropertiesFactory;
            this.packageSubfolder = packageSubfolder;
        }
        
        public void Install(RunningDeployment deployment)
        {
            Log.Info("Executing Commit To Git operation");
            var packageFiles = GetReferencedPackageFiles(deployment);

            var requiresPullRequest = RequiresPullRequest(deployment);
            var commitMessage = GenerateCommitMessage(deployment);
            
            var repositoryFactory = new RepositoryFactory(log, deployment.CurrentDirectory, pullRequestCreator);
            
            var argoProperties = customPropertiesFactory.Create<ArgoCDActionCustomProperties>();
            
            log.Info($"Found the following repository indicies '{argoProperties.Applications.Select(a => a.Name).Join(",")}'");
            foreach (var argoApplication in argoProperties.Applications)
            {
                var applicationSource = argoApplication.Sources.First();

                Log.Info($"Writing files to repository for '{argoApplication.Name}'");
                IGitConnection gitConnection = new GitConnection(applicationSource.Credentials!.Username, applicationSource.Credentials!.Password, applicationSource.RepoUrl.AbsoluteUri, new GitBranchName(applicationSource.TargetRevision));
                var repository = repositoryFactory.CloneRepository(argoApplication.Name, gitConnection);

                Log.Info($"Copying files into repository {applicationSource.RepoUrl}");
                var subFolder = deployment.Variables.Get(applicationSource.Path, String.Empty) ?? String.Empty;
                Log.VerboseFormat("Copying files into subfolder '{0}'", subFolder);

                var repositoryFiles = packageFiles.Select(f => new FileCopySpecification(f, repository.WorkingDirectory, subFolder)).ToList();
                
                CopyFiles(repositoryFiles);
                
                Log.Info("Staging files in repository");
                repository.StageFiles(repositoryFiles.Select(fcs => fcs.DestinationRelativePath).ToArray());
                
                Log.Info("Commiting changes");
                if (repository.CommitChanges(commitMessage))
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
            var fileGlobs = deployment.Variables.GetPaths(SpecialVariables.Git.TemplateGlobs);
            log.Info($"Selecting files from package using '{string.Join(" ", fileGlobs)}'");
            var filesToApply = SelectFiles(Path.Combine(deployment.CurrentDirectory, packageSubfolder), fileGlobs);
            log.Info($"Found {filesToApply.Length} files to apply");
            return filesToApply;
        }

        string GenerateCommitMessage(RunningDeployment deployment)
        {
            var summary = deployment.Variables.GetMandatoryVariable(SpecialVariables.Git.CommitMessageSummary);
            var description = deployment.Variables.Get(SpecialVariables.Git.CommitMessageDescription) ?? string.Empty;
            return description.Equals(string.Empty)
                ? summary
                : $"{summary}\n\n{description}";
        }
        
        IPackageRelativeFile[] SelectFiles(string pathToExtractedPackage, List<string> fileGlobs)
        {
            return fileGlobs.SelectMany(glob => fileSystem.EnumerateFilesWithGlob(pathToExtractedPackage, glob))
                            .Select(absoluteFilepath =>
                                    {
#if NETCORE
                                        var relativePath = Path.GetRelativePath(pathToExtractedPackage, file);
#else
                                        var relativePath = absoluteFilepath.Substring(pathToExtractedPackage.Length + 1);
#endif
                                        return new PackageRelativeFile(absoluteFilepath, relativePath);
                                    })
                            .ToArray<IPackageRelativeFile>();
        }
    }
}
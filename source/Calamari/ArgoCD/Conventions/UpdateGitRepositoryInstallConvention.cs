#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Dtos;
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
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateGitRepositoryInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly string packageSubfolder;
        readonly IGitHubPullRequestCreator pullRequestCreator;
        readonly ICustomPropertiesFactory customPropertiesFactory;
        readonly string customPropertiesFile;
        readonly string customPropertiesPassword;

        public UpdateGitRepositoryInstallConvention(ICalamariFileSystem fileSystem,
                                                    string packageSubfolder,
                                                    ILog log,
                                                    IGitHubPullRequestCreator pullRequestCreator,
                                                    ICustomPropertiesFactory customPropertiesFactory,
                                                    string customPropertiesFile,
                                                    string customPropertiesPassword)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            this.pullRequestCreator = pullRequestCreator;
            this.customPropertiesFactory = customPropertiesFactory;
            this.customPropertiesFile = customPropertiesFile;
            this.customPropertiesPassword = customPropertiesPassword;
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

            var argoProperties = customPropertiesFactory.Create<ArgoCustomPropertiesDto>(customPropertiesFile, customPropertiesPassword);

            log.Info($"Found the following applications: '{argoProperties.Applications.Select(a => a.Name).Join(",")}'");

            foreach (var application in argoProperties.Applications)
            {
                foreach (var applicationSource in application.Sources)
                {
                    Log.Info($"Writing files to repository for '{applicationSource.Url}'");
                    IGitConnection gitConnection = new GitConnection(applicationSource.Username, applicationSource.Password, applicationSource.Url, new GitBranchName(applicationSource.TargetRevision));
                    var repository = repositoryFactory.CloneRepository("Foobar", gitConnection);

                    Log.Info($"Copying files into repository {applicationSource.Url}");
                    var subFolder = applicationSource.Path ?? String.Empty;
                    Log.VerboseFormat("Copying files into subfolder '{0}'", subFolder);

                    var repositoryFiles = packageFiles.Select(f => new FileCopySpecification(f, repository.WorkingDirectory, subFolder)).ToList();

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
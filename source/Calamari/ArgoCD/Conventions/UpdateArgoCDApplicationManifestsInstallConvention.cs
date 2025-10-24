#if NET
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.ArgoCD.Models;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDApplicationManifestsInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly string packageSubfolder;
        readonly IGitHubPullRequestCreator pullRequestCreator;
        readonly DeploymentConfigFactory deploymentConfigFactory;
        readonly ICustomPropertiesLoader customPropertiesLoader;
        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser;
        readonly IArgoCDManifestsFileMatcher argoCDManifestsFileMatcher;

        public UpdateArgoCDApplicationManifestsInstallConvention(ICalamariFileSystem fileSystem,
                                                                 string packageSubfolder,
                                                                 ILog log,
                                                                 IGitHubPullRequestCreator pullRequestCreator,
                                                                 DeploymentConfigFactory deploymentConfigFactory,
                                                                 ICustomPropertiesLoader customPropertiesLoader,
                                                                 IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
                                                                 IArgoCDManifestsFileMatcher argoCDManifestsFileMatcher)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            this.pullRequestCreator = pullRequestCreator;
            this.deploymentConfigFactory = deploymentConfigFactory;
            this.customPropertiesLoader = customPropertiesLoader;
            this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
            this.argoCDManifestsFileMatcher = argoCDManifestsFileMatcher;
            this.packageSubfolder = packageSubfolder;
        }

        public void Install(RunningDeployment deployment)
        {
            Log.Info("Executing Update Argo CD Application manifests operation");
            var deploymentConfig = deploymentConfigFactory.CreateCommitToGitConfig(deployment);
            var packageFiles = GetReferencedPackageFiles(deploymentConfig);

            var repositoryFactory = new RepositoryFactory(log, deployment.CurrentDirectory, pullRequestCreator);

            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();

            var gitCredentials = argoProperties.Credentials.ToDictionary(c => c.Url);
            var deploymentScope = deployment.Variables.GetDeploymentScope();

            if (argoProperties.Applications.Length == 0)
            {
                log.LogMissingAnnotationsWarning(deploymentScope);
                return;
            }
            
            log.Info($"Found the following applications: '{argoProperties.Applications.Select(a => a.Name).Join(",")}'");

            var repositoryNumber = 1;
            foreach (var application in argoProperties.Applications)
            {
                log.InfoFormat("Processing application {0}", application.Name);

                var instanceLinks = application.InstanceWebUiUrl != null ? new ArgoCDInstanceLinks(application.InstanceWebUiUrl) : null;
             
                var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);
                bool containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;
                var sourcesToInspect = applicationFromYaml.Spec.Sources.OfType<BasicSource>().ToList();

                LogWarningIfUpdatingMultipleSources(sourcesToInspect,
                                                    applicationFromYaml.Metadata.Annotations,
                                                    containsMultipleSources,
                                                    deploymentScope);
                
                var validationResult = ApplicationValidator.Validate(applicationFromYaml);
                validationResult.Action(log);
                
                var didUpdateSomething = false;
                foreach (var applicationSource in sourcesToInspect)
                {
                    var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources);
                    log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);

                    if (annotatedScope == deploymentScope)
                    {
                        Log.Info($"Writing files to repository '{applicationSource.RepoUrl}' for '{application.Name}'");

                        var gitCredential = gitCredentials.GetValueOrDefault(applicationSource.RepoUrl.AbsoluteUri);
                        if (gitCredential == null)
                        {
                            log.Info($"No Git credentials found for: '{applicationSource.RepoUrl.AbsoluteUri}', will attempt to clone repository anonymously.");
                        }

                        var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, applicationSource.RepoUrl.AbsoluteUri, new GitBranchName(applicationSource.TargetRevision));
                        var repository = repositoryFactory.CloneRepository(repositoryNumber.ToString(CultureInfo.InvariantCulture), gitConnection);

                        Log.Info($"Copying files into repository {applicationSource.RepoUrl}");
                        var subFolder = applicationSource.Path ?? string.Empty;
                        Log.VerboseFormat("Copying files into subfolder '{0}'", subFolder);

                        if (deploymentConfig.PurgeOutputDirectory)
                        {
                            repository.RecursivelyStageFilesForRemoval(subFolder);
                        }

                        var repositoryFiles = packageFiles.Select(f => new FileCopySpecification(f, repository.WorkingDirectory, subFolder)).ToList();
                        Log.VerboseFormat("Copying files into subfolder '{0}'", applicationSource.Path!);
                        CopyFiles(repositoryFiles);

                        Log.Info("Staging files in repository");
                        repository.StageFiles(repositoryFiles.Select(fcs => fcs.DestinationRelativePath).ToArray());

                        Log.Info("Commiting changes");
                        if (repository.CommitChanges(deploymentConfig.CommitParameters.Summary, deploymentConfig.CommitParameters.Description))
                        {
                            Log.Info("Changes were commited, pushing to remote");
                            repository.PushChanges(deploymentConfig.CommitParameters.RequiresPr,
                                                   deploymentConfig.CommitParameters.Summary,
                                                   deploymentConfig.CommitParameters.Description,
                                                   new GitBranchName(applicationSource.TargetRevision),
                                                   CancellationToken.None)
                                      .GetAwaiter()
                                      .GetResult();

                            didUpdateSomething = true;
                        }
                        else
                        {
                            Log.Info("No changes were commited.");
                        }

                        repositoryNumber++;
                    }
                }

                //if we have links, use that to generate a link, otherwise just put the name there
                var appName = instanceLinks != null
                    ? log.FormatLink(instanceLinks.ApplicationDetails(application.Name, application.KubernetesNamespace), application.Name)
                    : application.Name;

                var message = didUpdateSomething
                    ? "Updated Application {0}"
                    : "Nothing to update for Application {0}";
                
                log.InfoFormat(message, appName);
            }
        }

        void LogWarningIfUpdatingMultipleSources(List<BasicSource> sourcesToInspect,
                                                 Dictionary<string, string> applicationAnnotations,
                                                 bool containsMultipleSources,
                                                 (ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant) deploymentScope)
        {
            if (sourcesToInspect.Count > 1)
            {
                var sourcesWithScopes = sourcesToInspect.Select(s => (s, ScopingAnnotationReader.GetScopeForApplicationSource(s.Name.ToApplicationSourceName(), applicationAnnotations, containsMultipleSources))).ToList();
                var sourcesWithMatchingScopes = sourcesWithScopes.Where(s => s.Item2 == deploymentScope).ToList();

                if (sourcesWithMatchingScopes.Count > 1)
                {
                    log.Warn($"Multiple sources are associated with this deployment, they will all be updated with the same contents: {string.Join(", ", sourcesWithMatchingScopes.Select(s => s.s.Name))}");
                }
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
            log.Info($"Selecting files from package using '{config.InputSubPath}'");
            var filesToApply = SelectFiles(Path.Combine(config.WorkingDirectory, packageSubfolder), config);
            log.Info($"Found {filesToApply.Length} files to apply");
            return filesToApply;
        }

        IPackageRelativeFile[] SelectFiles(string pathToExtractedPackageFiles, ArgoCommitToGitConfig config) 
            => argoCDManifestsFileMatcher.FindMatchingPackageFiles(pathToExtractedPackageFiles, config.InputSubPath);
    }
}
#endif
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
using Microsoft.IdentityModel.Tokens;

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
            log.Verbose("Executing Update Argo CD Application manifests operation");
            var deploymentConfig = deploymentConfigFactory.CreateCommitToGitConfig(deployment);
            var packageFiles = GetReferencedPackageFiles(deploymentConfig);

            var repositoryFactory = new RepositoryFactory(log, fileSystem, deployment.CurrentDirectory, pullRequestCreator);

            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();

            var gitCredentials = argoProperties.Credentials.ToDictionary(c => c.Url);
            var deploymentScope = deployment.Variables.GetDeploymentScope();

            log.InfoFormat("Found {0} Argo CD applications to update", argoProperties.Applications.Length);
            foreach (var app in argoProperties.Applications)
            {
                log.VerboseFormat("- {0}", app.Name);
            }

            foreach (var application in argoProperties.Applications)
            {
                log.InfoFormat("Processing application {0}", application.Name);

                var instanceLinks = application.InstanceWebUiUrl != null ? new ArgoCDInstanceLinks(application.InstanceWebUiUrl) : null;

                var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);
                var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;
                var sourcesToInspect = applicationFromYaml.Spec.Sources;

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
                        log.Info($"Writing files to repository '{applicationSource.RepoUrl}' for '{application.Name}'");
                        string outputPath = CalculateOutputPath(applicationSource);

                        var gitCredential = gitCredentials.GetValueOrDefault(applicationSource.RepoUrl.AbsoluteUri);
                        if (gitCredential == null)
                        {
                            log.Info($"No Git credentials found for: '{applicationSource.RepoUrl.AbsoluteUri}', will attempt to clone repository anonymously.");
                        }

                        var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, applicationSource.RepoUrl, GitReference.CreateFromString(applicationSource.TargetRevision));

                        using (var repository = repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection))
                        {
                            var subFolder = outputPath;
                            log.VerboseFormat("Copying files into '{0}'", subFolder);

                            if (deploymentConfig.PurgeOutputDirectory)
                            {
                                repository.RecursivelyStageFilesForRemoval(subFolder);
                            }

                            var repositoryFiles = packageFiles.Select(f => new FileCopySpecification(f, repository.WorkingDirectory, subFolder)).ToList();
                            CopyFiles(repositoryFiles);

                            log.Info("Staging files in repository");
                            repository.StageFiles(repositoryFiles.Select(fcs => fcs.DestinationRelativePath).ToArray());

                            log.Info("Commiting changes");
                            if (repository.CommitChanges(deploymentConfig.CommitParameters.Summary, deploymentConfig.CommitParameters.Description))
                            {
                                log.Info("Changes were commited, pushing to remote");
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
                                log.Info("No changes were commited");
                            }
                        }
                    }
                    
                    //if we have links, use that to generate a link, otherwise just put the name there
                    var linkifiedAppName = instanceLinks != null
                        ? log.FormatLink(instanceLinks.ApplicationDetails(application.Name, application.KubernetesNamespace), application.Name)
                        : application.Name;

                    var message = didUpdateSomething
                        ? "Updated Application {0}"
                        : "Nothing to update for Application {0}";

                    log.InfoFormat(message, linkifiedAppName);
                }
            }
        }

        string CalculateOutputPath(SourceBase sourceToUpdate)
        {
            var sourceIdentity = sourceToUpdate.Name.IsNullOrEmpty() ? sourceToUpdate.RepoUrl.ToString() : sourceToUpdate.Name;
            if (sourceToUpdate is ReferenceSource)
            {
                if (!sourceToUpdate.Path.IsNullOrEmpty())
                {
                    log.WarnFormat("Unable to update ref source '{0}' as a path has been explicitly specified.", sourceIdentity);
                    log.Warn("Please split the source into separate sources and update annotations");
                    throw new CommandException("Unable to update a ref source with an explicit path");
                }
                return "/"; // always update ref sources from the root
            }
                        
            if (sourceToUpdate.Path is null)
            {
                log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceIdentity);
                throw new CommandException("Unable to update source due to missing path.");
            }

            return sourceToUpdate.Path;
        }

        void LogWarningIfUpdatingMultipleSources(List<SourceBase> sourcesToInspect,
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
                log.VerboseFormat($"Copying '{file.SourceAbsolutePath}' to '{file.DestinationAbsolutePath}'");
                EnsureParentDirectoryExists(file.DestinationAbsolutePath);
                fileSystem.CopyFile(file.SourceAbsolutePath, file.DestinationAbsolutePath);
            }
        }

        void EnsureParentDirectoryExists(string filePath)
        {
            var destinationDirectory = Path.GetDirectoryName(filePath);
            if (destinationDirectory != null)
            {
                fileSystem.CreateDirectory(destinationDirectory);
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
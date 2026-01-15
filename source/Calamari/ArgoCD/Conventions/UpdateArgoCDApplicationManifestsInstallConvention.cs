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
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using Calamari.ArgoCD.GitHub;
using Calamari.ArgoCD.Models;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDApplicationManifestsInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly string packageSubfolder;
        readonly DeploymentConfigFactory deploymentConfigFactory;
        readonly ICustomPropertiesLoader customPropertiesLoader;
        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser;
        readonly IArgoCDManifestsFileMatcher argoCDManifestsFileMatcher;
        readonly IGitVendorAgnosticApiAdapterFactory gitVendorAgnosticApiAdapterFactory;

        public UpdateArgoCDApplicationManifestsInstallConvention(ICalamariFileSystem fileSystem,
                                                                 string packageSubfolder,
                                                                 ILog log,
                                                                 DeploymentConfigFactory deploymentConfigFactory,
                                                                 ICustomPropertiesLoader customPropertiesLoader,
                                                                 IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
                                                                 IArgoCDManifestsFileMatcher argoCDManifestsFileMatcher,
                                                                 IGitVendorAgnosticApiAdapterFactory gitVendorAgnosticApiAdapterFactory)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            this.deploymentConfigFactory = deploymentConfigFactory;
            this.customPropertiesLoader = customPropertiesLoader;
            this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
            this.argoCDManifestsFileMatcher = argoCDManifestsFileMatcher;
            this.gitVendorAgnosticApiAdapterFactory = gitVendorAgnosticApiAdapterFactory;
            this.packageSubfolder = packageSubfolder;
        }

        public void Install(RunningDeployment deployment)
        {
            log.Verbose("Executing Update Argo CD Application manifests operation");
            var deploymentConfig = deploymentConfigFactory.CreateCommitToGitConfig(deployment);
            var packageFiles = GetReferencedPackageFiles(deploymentConfig);

            var repositoryFactory = new RepositoryFactory(log, fileSystem, deployment.CurrentDirectory, gitVendorAgnosticApiAdapterFactory);

            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();

            var gitCredentials = argoProperties.Credentials.ToDictionary(c => c.Url);
            var deploymentScope = deployment.Variables.GetDeploymentScope();

            log.LogApplicationCounts(deploymentScope, argoProperties.Applications);

            var updatedApplicationsWithSources = new Dictionary<ApplicationName, HashSet<ApplicationSourceName?>>();
            var totalApplicationsWithSourceCounts = new List<(ApplicationName, int, int)>();
            var gitReposUpdated = new HashSet<string>();

            foreach (var application in argoProperties.Applications)
            {
                log.InfoFormat("Processing application {0}", application.Name);

                var instanceLinks = application.InstanceWebUiUrl != null ? new ArgoCDInstanceLinks(application.InstanceWebUiUrl) : null;

                var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);
                var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;
                totalApplicationsWithSourceCounts.Add((applicationFromYaml.Metadata.Name.ToApplicationName(), 
                                                       applicationFromYaml.Spec.Sources.Count, 
                                                       applicationFromYaml.Spec.Sources.Count(s => ScopingAnnotationReader.GetScopeForApplicationSource(s.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources) == deploymentScope)));
                
                LogWarningIfUpdatingMultipleSources(applicationFromYaml.Spec.Sources,
                                                    applicationFromYaml.Metadata.Annotations,
                                                    containsMultipleSources,
                                                    deploymentScope);

                var validationResult = ApplicationValidator.Validate(applicationFromYaml);
                validationResult.Action(log);

                var didUpdateSomething = false;
                foreach (var applicationSourceWithMetadata in applicationFromYaml.GetSourcesWithMetadata())
                {
                    var applicationSource = applicationSourceWithMetadata.Source;

                    var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources);
                    log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);

                    if (annotatedScope == deploymentScope)
                    {
                        var sourceIdentity = applicationSource.Name.IsNullOrEmpty() ? applicationSource.RepoUrl.ToString() : applicationSource.Name;
                        if (applicationSourceWithMetadata.SourceType == null)
                        {
                            log.WarnFormat("Unable to update source '{0}' as its source type was not detected by Argo CD.", sourceIdentity);
                            continue;   
                        }
                        
                        log.Info($"Writing files to repository '{applicationSource.RepoUrl}' for '{application.Name}'");

                        if (!TryCalculateOutputPath(applicationSource, out var outputPath))
                        {
                            continue;
                        }

                        var gitCredential = gitCredentials.GetValueOrDefault(applicationSource.RepoUrl.AbsoluteUri);
                        if (gitCredential == null)
                        {
                            log.Info($"No Git credentials found for: '{applicationSource.RepoUrl.AbsoluteUri}', will attempt to clone repository anonymously.");
                        }

                        var targetBranch = GitReference.CreateFromString(applicationSource.TargetRevision);
                        var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, applicationSource.RepoUrl, targetBranch);

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
                                                       targetBranch,
                                                       CancellationToken.None)
                                          .GetAwaiter()
                                          .GetResult();

                                didUpdateSomething = true;
                                updatedApplicationsWithSources.GetOrAdd(applicationFromYaml.Metadata.Name.ToApplicationName(), _ => new HashSet<ApplicationSourceName?>()).Add(applicationSource.Name.ToApplicationSourceName());
                                gitReposUpdated.Add(applicationSource.RepoUrl.AbsoluteUri);
                            }
                            else
                            {
                                log.Info("No changes were commited");
                            }
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
            
            var gatewayIds = argoProperties.Applications.Select(a => a.GatewayId).ToHashSet();
            var outputWriter = new ArgoCDOutputVariablesWriter(log);
            outputWriter.WriteManifestUpdateOutput(gatewayIds,
                                                gitReposUpdated,
                                                totalApplicationsWithSourceCounts,
                                                updatedApplicationsWithSources.Select(kv => (kv.Key, kv.Value.Count)).ToArray()
                                               );

        }

        bool TryCalculateOutputPath(ApplicationSource sourceToUpdate, out string outputPath)
        {
            outputPath = "";
            var sourceIdentity = string.IsNullOrEmpty(sourceToUpdate.Name) ? sourceToUpdate.RepoUrl.ToString() : sourceToUpdate.Name;
            if (sourceToUpdate.Ref != null)
            {
                if (sourceToUpdate.Path != null)
                {
                    log.WarnFormat("Unable to update ref source '{0}' as a path has been explicitly specified.", sourceIdentity);
                    log.Warn("Please split the source into separate sources and update annotations.");
                    return false;
                }
                return true;
            }
                        
            if (sourceToUpdate.Path == null)
            {
                log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceIdentity);
                return false;
            }
            outputPath = sourceToUpdate.Path;
            return true;
        }

        //TODO(tmm): should we be removing this warning now
        void LogWarningIfUpdatingMultipleSources(List<ApplicationSource> sourcesToInspect,
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


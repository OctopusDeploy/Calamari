#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Time;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppImagesInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly DeploymentConfigFactory deploymentConfigFactory;
        readonly ICommitMessageGenerator commitMessageGenerator;
        readonly ICustomPropertiesLoader customPropertiesLoader;
        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser;
        readonly IGitVendorAgnosticApiAdapterFactory gitVendorAgnosticApiAdapterFactory;
        readonly IClock clock;

        public UpdateArgoCDAppImagesInstallConvention(ILog log,
                                                      ICalamariFileSystem fileSystem,
                                                      DeploymentConfigFactory deploymentConfigFactory,
                                                      ICommitMessageGenerator commitMessageGenerator,
                                                      ICustomPropertiesLoader customPropertiesLoader,
                                                      IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
                                                      IGitVendorAgnosticApiAdapterFactory gitVendorAgnosticApiAdapterFactory,
                                                      IClock clock)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.deploymentConfigFactory = deploymentConfigFactory;
            this.commitMessageGenerator = commitMessageGenerator;
            this.customPropertiesLoader = customPropertiesLoader;
            this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
            this.gitVendorAgnosticApiAdapterFactory = gitVendorAgnosticApiAdapterFactory;
            this.clock = clock;
        }

        public void Install(RunningDeployment deployment)
        {
            log.Verbose("Executing Update Argo CD Application Images");
            var deploymentConfig = deploymentConfigFactory.CreateUpdateImageConfig(deployment);

            var repositoryFactory = new RepositoryFactory(log, fileSystem, deployment.CurrentDirectory, gitVendorAgnosticApiAdapterFactory, clock);

            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();

            var gitCredentials = argoProperties.Credentials.ToDictionary(c => c.Url);
            var deploymentScope = deployment.Variables.GetDeploymentScope();

            log.LogApplicationCounts(deploymentScope, argoProperties.Applications);

            var applicationResults = argoProperties.Applications
                                                   .Select(application =>
                                                               ProcessApplication(application,
                                                                                  deploymentScope,
                                                                                  gitCredentials,
                                                                                  repositoryFactory,
                                                                                  deploymentConfig))
                                                   .ToList();

            var totalApplicationsWithSourceCounts = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApplications = applicationResults.Where(r => r.Updated).ToList();
            var updatedApplicationsWithSources = updatedApplications.Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();
            var gitReposUpdated = updatedApplications.SelectMany(r => r.GitReposUpdated).ToHashSet();
            var newImagesWritten = updatedApplications.SelectMany(r => r.UpdatedImages).ToHashSet();

            var gatewayIds = argoProperties.Applications.Select(a => a.GatewayId).ToHashSet();
            var outputWriter = new ArgoCDOutputVariablesWriter(log);
            outputWriter.WriteImageUpdateOutput(gatewayIds,
                                                gitReposUpdated,
                                                totalApplicationsWithSourceCounts,
                                                updatedApplicationsWithSources,
                                                newImagesWritten.Count
                                               );
        }

        ProcessApplicationResult ProcessApplication(ArgoCDApplicationDto application,
                                                    DeploymentScope deploymentScope,
                                                    Dictionary<string, GitCredentialDto> gitCredentials,
                                                    RepositoryFactory repositoryFactory,
                                                    UpdateArgoCDAppDeploymentConfig deploymentConfig)
        {
            log.InfoFormat("Processing application {0}", application.Name);
            var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);
            var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;
            var applicationName = applicationFromYaml.Metadata.Name;
            
            var validationResult = ApplicationValidator.Validate(applicationFromYaml);
            validationResult.Action(log);

            var updatedSourcesResults = applicationFromYaml.GetSourcesWithMetadata()
                                                           .Select(applicationSource => new
                                                           {
                                                               Updated = ProcessSource(applicationSource,
                                                                                       applicationFromYaml,
                                                                                       containsMultipleSources,
                                                                                       deploymentScope,
                                                                                       gitCredentials,
                                                                                       repositoryFactory,
                                                                                       deploymentConfig,
                                                                                       application.DefaultRegistry),
                                                               applicationSource,
                                                           })
                                                           .Where(r => r.Updated.Any())
                                                           .ToList();


            //if we have links, use that to generate a link, otherwise just put the name there
            var instanceLinks = application.InstanceWebUiUrl != null ? new ArgoCDInstanceLinks(application.InstanceWebUiUrl) : null;
            var linkifiedAppName = instanceLinks != null
                ? log.FormatLink(instanceLinks.ApplicationDetails(applicationName, application.KubernetesNamespace), applicationName)
                : applicationName;

            var message = updatedSourcesResults.Any()
                ? "Updated Application {0}"
                : "Nothing to update for Application {0}";

            log.InfoFormat(message, linkifiedAppName);

            return new ProcessApplicationResult(applicationName.ToApplicationName())
            {
                UpdatedSourceCount = updatedSourcesResults.Count,
                TotalSourceCount = applicationFromYaml.Spec.Sources.Count,
                MatchingSourceCount = applicationFromYaml.Spec.Sources.Count(s => deploymentScope.Matches(ScopingAnnotationReader.GetScopeForApplicationSource(s.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources))),
                GitReposUpdated = updatedSourcesResults.Select(r => r.applicationSource.Source.RepoUrl.AbsoluteUri).ToHashSet(),
                UpdatedImages = updatedSourcesResults.SelectMany(r => r.Updated).ToHashSet()
            };
        }

        /// <returns>Images that were updated</returns>
        HashSet<string> ProcessSource(ApplicationSourceWithMetadata sourceWithMetadata,
                                                     Application applicationFromYaml,
                                                     bool containsMultipleSources,
                                                     DeploymentScope deploymentScope,
                                                     Dictionary<string, GitCredentialDto> gitCredentials,
                                                     RepositoryFactory repositoryFactory,
                                                     UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                                     string defaultRegistry)
        {

            var applicationSource = sourceWithMetadata.Source;
            var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources);

            log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);
            if (!deploymentScope.Matches(annotatedScope))
                return new HashSet<string>();

            switch (sourceWithMetadata.SourceType)
            {
                case SourceType.Directory:
                {
                    return applicationSource.Ref != null
                        ? ProcessRef(applicationFromYaml,
                                     gitCredentials,
                                     repositoryFactory,
                                     deploymentConfig,
                                     sourceWithMetadata,
                                     defaultRegistry)
                        : ProcessDirectory(gitCredentials,
                                           repositoryFactory,
                                           deploymentConfig,
                                           sourceWithMetadata,
                                           defaultRegistry);
                }
                case SourceType.Helm:
                {
                    return ProcessHelm(applicationFromYaml,
                                       sourceWithMetadata,
                                       gitCredentials,
                                       repositoryFactory,
                                       deploymentConfig,
                                       defaultRegistry);
                }
                case SourceType.Kustomize:
                {
                    return ProcessKustomize(gitCredentials,
                                            repositoryFactory,
                                            deploymentConfig,
                                            sourceWithMetadata,
                                            defaultRegistry);
                }
                case SourceType.Plugin:
                {
                    log.WarnFormat("Unable to update source '{0}' as Plugin sources aren't currently supported.", sourceWithMetadata.SourceIdentity);
                    return new HashSet<string>();
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <returns>Images that were updated</returns>
        HashSet<string> ProcessKustomize(Dictionary<string, GitCredentialDto> gitCredentials,
                                                        RepositoryFactory repositoryFactory,
                                                        UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                                        ApplicationSourceWithMetadata sourceWithMetadata,
                                                        string defaultRegistry)
        {
            var applicationSource = sourceWithMetadata.Source;

            if (applicationSource.Path == null)
            {
                log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
                return new HashSet<string>();
            }

            using (var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory))
            {
                log.Verbose($"Reading files from {applicationSource.Path}");

                var (updatedFiles, updatedImages) = UpdateKustomizeYaml(repository.WorkingDirectory, applicationSource.Path!, defaultRegistry, deploymentConfig.ImageReferences);
                if (updatedImages.Count > 0)
                {
                    var didPush = PushToRemote(repository,
                                               GitReference.CreateFromString(applicationSource.TargetRevision),
                                               deploymentConfig.CommitParameters,
                                               updatedFiles,
                                               updatedImages);

                    if (didPush)
                    {
                        return updatedImages;
                    }
                }
            }

            return new HashSet<string>();
        }

        /// <returns>Images that were updated</returns>
        HashSet<string> ProcessRef(Application applicationFromYaml,
                                                  Dictionary<string, GitCredentialDto> gitCredentials,
                                                  RepositoryFactory repositoryFactory,
                                                  UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                                  ApplicationSourceWithMetadata sourceWithMetadata,
                                                  string defaultRegistry)
        {
            var applicationSource = sourceWithMetadata.Source;
            
            if (applicationSource.Path != null)
            {
                log.WarnFormat("The source '{0}' contains a Ref, only referenced files will be updated. Please create another source with the same URL if you wish to update files under the path.", sourceWithMetadata.SourceIdentity);
            }

            var helmTargetsForRefSource = new HelmValuesFileUpdateTargetParser(applicationFromYaml, defaultRegistry)
                .GetHelmTargetsForRefSource(sourceWithMetadata);

            LogHelmSourceConfigurationProblems(helmTargetsForRefSource.Problems);

            using var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory);
            var updatedImages = ProcessHelmUpdateTargets(repository,
                                                         deploymentConfig,
                                                         applicationSource,
                                                         helmTargetsForRefSource.Targets);

            return updatedImages;
        }
        
        /// <returns>Images that were updated</returns>
        HashSet<string> ProcessDirectory(Dictionary<string, GitCredentialDto> gitCredentials,
                                                        RepositoryFactory repositoryFactory,
                                                        UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                                        ApplicationSourceWithMetadata sourceWithMetadata,
                                                        string defaultRegistry)
        {
            var applicationSource = sourceWithMetadata.Source;
            if (applicationSource.Path == null)
            {
                log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
                return new HashSet<string>();
            }

            using (var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory))
            {
                log.Verbose($"Reading files from {applicationSource.Path}");

                var (updatedFiles, updatedImages) = UpdateKubernetesYaml(repository.WorkingDirectory, applicationSource.Path!, defaultRegistry, deploymentConfig.ImageReferences);
                if (updatedImages.Count > 0)
                {
                    var didPush = PushToRemote(repository,
                                               GitReference.CreateFromString(applicationSource.TargetRevision),
                                               deploymentConfig.CommitParameters,
                                               updatedFiles,
                                               updatedImages);

                    return didPush ? updatedImages : new HashSet<string>();
                }
            }
            return new HashSet<string>();
        }

        /// <returns>Images that were updated</returns>
        HashSet<string> ProcessHelm(Application applicationFromYaml,
                                                   ApplicationSourceWithMetadata sourceWithMetadata,
                                                   Dictionary<string, GitCredentialDto> gitCredentials,
                                                   RepositoryFactory repositoryFactory,
                                                   UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                                   string defaultRegistry)
        {
            var applicationSource = sourceWithMetadata.Source;
            
            if (applicationSource.Path == null)
            {
                log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
                return new HashSet<string>();
            }

            var explicitHelmSources = new HelmValuesFileUpdateTargetParser(applicationFromYaml, defaultRegistry)
                .GetExplicitValuesFilesToUpdate(sourceWithMetadata);

            var valuesFilesToUpdate = new List<HelmValuesFileImageUpdateTarget>(explicitHelmSources.Targets);
            var valueFileProblems = new HashSet<HelmSourceConfigurationProblem>(explicitHelmSources.Problems);

            //Add the implicit value file if needed
            using var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory);
            var repoSubPath = Path.Combine(repository.WorkingDirectory, applicationSource.Path!);
            var implicitValuesFile = HelmDiscovery.TryFindValuesFile(fileSystem, repoSubPath);
            if (implicitValuesFile != null && explicitHelmSources.Targets.None(t => t.FileName == implicitValuesFile))
            {
                var (target, problem) = AddImplicitValuesFile(applicationFromYaml,
                                                              sourceWithMetadata,
                                                              implicitValuesFile,
                                                              defaultRegistry);
                if (target != null)
                    valuesFilesToUpdate.Add(target);

                if (problem != null)
                    valueFileProblems.Add(problem);
            }

            LogHelmSourceConfigurationProblems(valueFileProblems);

            return ProcessHelmUpdateTargets(repository,
                                            deploymentConfig,
                                            applicationSource,
                                            valuesFilesToUpdate);
        }

        /// <returns>Images that were updated</returns>
        HashSet<string> ProcessHelmUpdateTargets(RepositoryWrapper repository,
                                                 UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                                 ApplicationSource source,
                                                 IReadOnlyCollection<HelmValuesFileImageUpdateTarget> targets)
        {
            var results = targets.Select(t => UpdateHelmImageValues(repository.WorkingDirectory,
                                                                    t,
                                                                    deploymentConfig.ImageReferences
                                                                   ))
                                 .ToList();

            var updatedImages = results.SelectMany(r => r.ImagesUpdated).ToHashSet();
            if (updatedImages.Count > 0)
            {
                var didPush = PushToRemote(repository,
                                           GitReference.CreateFromString(source.TargetRevision),
                                           deploymentConfig.CommitParameters,
                                           results.Where(r => r.ImagesUpdated.Any()).Select(r => r.RelativeFilepath).ToHashSet(),
                                           updatedImages);

                if (didPush)
                {
                    return updatedImages;
                }
            }

            return new HashSet<string>();
        }

        void LogHelmSourceConfigurationProblems(IReadOnlyCollection<HelmSourceConfigurationProblem> helmSourceConfigurationProblems)
        {
            foreach (var helmSourceConfigurationProblem in helmSourceConfigurationProblems)
            {
                LogProblem(helmSourceConfigurationProblem);
            }

            void LogProblem(HelmSourceConfigurationProblem helmSourceConfigurationProblem)
            {
                switch (helmSourceConfigurationProblem)
                {
                    case HelmSourceIsMissingImagePathAnnotation helmSourceIsMissingImagePathAnnotation:
                    {
                        if (helmSourceIsMissingImagePathAnnotation.RefSourceIdentity == null)
                        {
                            log.WarnFormat("The Helm source '{0}' is missing an annotation for the image replace path. It will not be updated.",
                                           helmSourceIsMissingImagePathAnnotation.SourceIdentity);
                        }
                        else
                        {
                            log.WarnFormat("The Helm source '{0}' is missing an annotation for the image replace path. The source '{1}' will not be updated.",
                                           helmSourceIsMissingImagePathAnnotation.SourceIdentity,
                                           helmSourceIsMissingImagePathAnnotation.RefSourceIdentity);
                        }

                        log.WarnFormat("Annotation creation documentation can be found {0}.", log.FormatShortLink("argo-cd-helm-image-annotations", "here"));

                        return;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(helmSourceConfigurationProblem));
                }
            }
        }

        RepositoryWrapper CreateRepository(Dictionary<string, GitCredentialDto> gitCredentials, ApplicationSource source, RepositoryFactory repositoryFactory)
        {
            var gitCredential = gitCredentials.GetValueOrDefault(source.RepoUrl.AbsoluteUri);
            if (gitCredential == null)
            {
                log.Info($"No Git credentials found for: '{source.RepoUrl.AbsoluteUri}', will attempt to clone repository anonymously.");
            }

            var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, new Uri(source.RepoUrl.AbsoluteUri), GitReference.CreateFromString(source.TargetRevision));
            return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
        }

        (HelmValuesFileImageUpdateTarget? Target, HelmSourceConfigurationProblem? Problem) AddImplicitValuesFile(Application applicationFromYaml,
                                                                                                                 ApplicationSourceWithMetadata applicationSource,
                                                                                                                 string valuesFilename,
                                                                                                                 string defaultRegistry)
        {
            var imageReplacePaths = ScopingAnnotationReader.GetImageReplacePathsForApplicationSource(
                                                                                                     applicationSource.Source.Name.ToApplicationSourceName(),
                                                                                                     applicationFromYaml.Metadata.Annotations,
                                                                                                     applicationFromYaml.Spec.Sources.Count > 1);
            if (!imageReplacePaths.Any())
            {
                return (null, new HelmSourceIsMissingImagePathAnnotation(applicationSource.SourceIdentity));
            }

            return (new HelmValuesFileImageUpdateTarget(
                                                        applicationFromYaml.Metadata.Name.ToApplicationName(),
                                                        applicationSource.Source.Name.ToApplicationSourceName(),
                                                        defaultRegistry,
                                                        applicationSource.Source.Path,
                                                        applicationSource.Source.RepoUrl,
                                                        applicationSource.Source.TargetRevision,
                                                        valuesFilename,
                                                        imageReplacePaths), null);
        }

        (HashSet<string>, HashSet<string>) UpdateKubernetesYaml(string rootPath,
                                                                string subFolder,
                                                                string defaultRegistry,
                                                                List<ContainerImageReference> imagesToUpdate)
        {
            var absSubFolder = Path.Combine(rootPath, subFolder);

            var filesToUpdate = FindYamlFiles(absSubFolder).ToHashSet();
            Func<string, IContainerImageReplacer> imageReplacerFactory = yaml => new ContainerImageReplacer(yaml, defaultRegistry);
            log.Verbose($"Found {filesToUpdate.Count} yaml files to process");

            return Update(rootPath, imagesToUpdate, filesToUpdate, imageReplacerFactory);
        }

        
        (HashSet<string>, HashSet<string>) UpdateKustomizeYaml(string rootPath,
                                                               string subFolder,
                                                               string defaultRegistry,
                                                               List<ContainerImageReference> imagesToUpdate)
        {
            var absSubFolder = Path.Combine(rootPath, subFolder);

            Func<string, IContainerImageReplacer> imageReplacerFactory;
            HashSet<string> filesToUpdate;

            var kustomizationFile = KustomizeDiscovery.TryFindKustomizationFile(fileSystem, absSubFolder);
            if (kustomizationFile != null)
            {
                filesToUpdate = new HashSet<string> { kustomizationFile };
                imageReplacerFactory = yaml => new KustomizeImageReplacer(yaml, defaultRegistry, log);
                log.Verbose("kustomization file found, will only update images transformer in the kustomization file");
                return Update(rootPath, imagesToUpdate, filesToUpdate, imageReplacerFactory);
            }

            log.Warn("kustomization file not found, no files will be updated");
            return (new HashSet<string>(), new HashSet<string>());
        }

        (HashSet<string>, HashSet<string>) Update(string rootPath, List<ContainerImageReference> imagesToUpdate, HashSet<string> filesToUpdate, Func<string, IContainerImageReplacer> imageReplacerFactory)
        {
            var updatedFiles = new HashSet<string>();
            var updatedImages = new HashSet<string>();
            foreach (var file in filesToUpdate)
            {
                var relativePath = Path.GetRelativePath(rootPath, file);
                log.Verbose($"Processing file {relativePath}.");
                var content = fileSystem.ReadFile(file);

                var imageReplacer = imageReplacerFactory(content);
                var imageReplacementResult = imageReplacer.UpdateImages(imagesToUpdate);

                if (imageReplacementResult.UpdatedImageReferences.Count > 0)
                {
                    fileSystem.OverwriteFile(file, imageReplacementResult.UpdatedContents);
                    updatedImages.UnionWith(imageReplacementResult.UpdatedImageReferences);
                    updatedFiles.Add(relativePath);
                    log.Verbose($"Updating file {relativePath} with new image references.");
                    foreach (var change in imageReplacementResult.UpdatedImageReferences)
                    {
                        log.Verbose($"Updated image reference: {change}");
                    }
                }
                else
                {
                    log.Verbose($"No changes made to file {relativePath} as no image references were updated.");
                }
            }

            return (updatedFiles, updatedImages);
        }

        HelmRefUpdatedResult UpdateHelmImageValues(string rootPath,
                                                   HelmValuesFileImageUpdateTarget target,
                                                   List<ContainerImageReference> imagesToUpdate)
        {
            var filepath = Path.Combine(rootPath, target.Path, target.FileName);
            log.Info($"Processing file at {filepath}.");
            var fileContent = fileSystem.ReadFile(filepath);
            var helmImageReplacer = new HelmContainerImageReplacer(fileContent, target.DefaultClusterRegistry, target.ImagePathDefinitions, log);
            var imageUpdateResult = helmImageReplacer.UpdateImages(imagesToUpdate);

            if (imageUpdateResult.UpdatedImageReferences.Count > 0)
            {
                fileSystem.OverwriteFile(filepath, imageUpdateResult.UpdatedContents);
                try
                {
                    return new HelmRefUpdatedResult(imageUpdateResult.UpdatedImageReferences, Path.Combine(target.Path, target.FileName));
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to commit changes to the Git Repository: {ex.Message}");
                    throw;
                }
            }

            return new HelmRefUpdatedResult(new HashSet<string>(), Path.Combine(target.Path, target.FileName));
        }

        bool PushToRemote(RepositoryWrapper repository,
                          GitReference branchName,
                          GitCommitParameters commitParameters,
                          HashSet<string> updatedFiles,
                          HashSet<string> updatedImages)
        {
            log.Info("Staging files in repository");
            repository.StageFiles(updatedFiles.ToArray());

            var commitDescription = commitMessageGenerator.GenerateDescription(updatedImages, commitParameters.Description);

            log.Info("Commiting changes");
            if (!repository.CommitChanges(commitParameters.Summary, commitDescription))
                return false;

            log.Verbose("Pushing to remote");
            repository.PushChanges(commitParameters.RequiresPr,
                                   commitParameters.Summary,
                                   commitDescription,
                                   branchName,
                                   CancellationToken.None)
                      .GetAwaiter()
                      .GetResult();

            return true;
        }

        //NOTE: rootPath needs to include the subfolder
        IEnumerable<string> FindYamlFiles(string rootPath)
        {
            var yamlFileGlob = "**/*.{yaml,yml}";
            return fileSystem.EnumerateFilesWithGlob(rootPath, yamlFileGlob);
        }

        class ProcessApplicationResult
        {
            public ProcessApplicationResult(ApplicationName applicationName)
            {
                ApplicationName = applicationName;
            }

            public int TotalSourceCount { get; set; }
            public int MatchingSourceCount { get; set; }
            public HashSet<string> GitReposUpdated { get; set; } = new HashSet<string>();
            public ApplicationName ApplicationName { get; }
            public HashSet<string> UpdatedImages { get; set; } = new HashSet<string>();

            public int UpdatedSourceCount { get; set; }
            public bool Updated => UpdatedSourceCount > 0;
        }
    }
}

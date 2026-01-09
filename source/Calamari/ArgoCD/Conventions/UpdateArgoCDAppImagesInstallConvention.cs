#if NET
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
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
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

        public UpdateArgoCDAppImagesInstallConvention(ILog log,
                                                      ICalamariFileSystem fileSystem,
                                                      DeploymentConfigFactory deploymentConfigFactory,
                                                      ICommitMessageGenerator commitMessageGenerator,
                                                      ICustomPropertiesLoader customPropertiesLoader,
                                                      IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
                                                      IGitVendorAgnosticApiAdapterFactory gitVendorAgnosticApiAdapterFactory)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.deploymentConfigFactory = deploymentConfigFactory;
            this.commitMessageGenerator = commitMessageGenerator;
            this.customPropertiesLoader = customPropertiesLoader;
            this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
            this.gitVendorAgnosticApiAdapterFactory = gitVendorAgnosticApiAdapterFactory;
        }

        public void Install(RunningDeployment deployment)
        {
            log.Verbose("Executing Update Argo CD Application Images");
            var deploymentConfig = deploymentConfigFactory.CreateUpdateImageConfig(deployment);

            var repositoryFactory = new RepositoryFactory(log, fileSystem, deployment.CurrentDirectory, gitVendorAgnosticApiAdapterFactory);

            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();

            var gitCredentials = argoProperties.Credentials.ToDictionary(c => c.Url);
            var deploymentScope = deployment.Variables.GetDeploymentScope();
            
            log.LogApplicationCounts(deploymentScope, argoProperties.Applications);

            var updatedApplicationsWithSources = new Dictionary<ApplicationName, HashSet<ApplicationSourceName?>>();
            var totalApplicationsWithSourceCounts = new List<(ApplicationName, int, int)>();
            var newImagesWritten = new HashSet<string>();
            var gitReposUpdated = new HashSet<string>();
            foreach (var application in argoProperties.Applications)
            {
                log.InfoFormat("Processing application {0}", application.Name);

                var instanceLinks = application.InstanceWebUiUrl != null ? new ArgoCDInstanceLinks(application.InstanceWebUiUrl) : null;

                var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);

                var validationResult = ApplicationValidator.Validate(applicationFromYaml);
                validationResult.Action(log);

                var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;
                totalApplicationsWithSourceCounts.Add((applicationFromYaml.Metadata.Name.ToApplicationName(), 
                                                       applicationFromYaml.Spec.Sources.Count, 
                                                       applicationFromYaml.Spec.Sources.Count(s => ScopingAnnotationReader.GetScopeForApplicationSource(s.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources) == deploymentScope)));

                var didUpdateSomething = false;
                foreach (var applicationSource in applicationFromYaml.GetSourcesWithMetadata())
                {
                    didUpdateSomething |= WorkOnSource(applicationSource, applicationFromYaml, containsMultipleSources, deploymentScope,
                                                      application,
                                                      gitCredentials,
                                                      repositoryFactory,
                                                      deploymentConfig,
                                                      newImagesWritten,
                                                      updatedApplicationsWithSources,
                                                      gitReposUpdated);
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
            outputWriter.WriteImageUpdateOutput(gatewayIds,
                                                gitReposUpdated,
                                                totalApplicationsWithSourceCounts,
                                                updatedApplicationsWithSources.Select(kv => (kv.Key, kv.Value.Count)).ToArray(),
                                                newImagesWritten.Count
                                               );
        }

        bool WorkOnSource(ApplicationSourceWithMetadata sourceWithMetadata,
                          Application applicationFromYaml,
                          bool containsMultipleSources,
                          (ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant) deploymentScope,
                          ArgoCDApplicationDto application,
                          Dictionary<string, GitCredentialDto> gitCredentials,
                          RepositoryFactory repositoryFactory,
                          UpdateArgoCDAppDeploymentConfig deploymentConfig,
                          HashSet<string> newImagesWritten,
                          Dictionary<ApplicationName, HashSet<ApplicationSourceName?>> updatedApplicationsWithSources,
                          HashSet<string> gitReposUpdated)
        {
            var applicationSource = sourceWithMetadata.Source;
            var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources);
            log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);
            if (annotatedScope == deploymentScope)
            {
                var sourceIdentity = applicationSource.Name.IsNullOrEmpty() ? applicationSource.RepoUrl.ToString() : applicationSource.Name;
                if (sourceWithMetadata.SourceType == null)
                {
                    log.WarnFormat("Unable to update source '{0}' as its source type was not detected by Argo CD.", sourceIdentity);
                    return false;
                }

                switch (sourceWithMetadata.SourceType)
                {
                    case SourceType.Directory:
                    {
                        if (applicationSource.Ref != null)
                        {
                            bool didUpdateSomething = false;

                            var helmTargetsForRefSource = new HelmValuesFileUpdateTargetParser(applicationFromYaml, application.DefaultRegistry)
                                .GetHelmTargetsForRefSource(applicationSource);

                            LogHelmSourceConfigurationProblems(helmTargetsForRefSource.Problems, applicationFromYaml.Metadata.Annotations, containsMultipleSources, deploymentScope);

                            foreach (var valuesFileSource in helmTargetsForRefSource.Targets)
                            {
                                didUpdateSomething |= WorkOnHelmUpdateTarget(applicationFromYaml,
                                                                          gitCredentials,
                                                                          repositoryFactory,
                                                                          deploymentConfig,
                                                                          newImagesWritten,
                                                                          updatedApplicationsWithSources,
                                                                          gitReposUpdated,
                                                                          valuesFileSource);
                            }

                            return didUpdateSomething;
                        }
                        else
                        {
                            using (var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory))
                            {
                                var repoSubPath = Path.Combine(repository.WorkingDirectory, applicationSource.Path!);
                                log.Verbose($"Reading files from {applicationSource.Path}");

                                var (updatedFiles, updatedImages) = UpdateKubernetesYaml(repository.WorkingDirectory, applicationSource.Path!, application.DefaultRegistry, deploymentConfig.ImageReferences);
                                if (updatedImages.Count > 0)
                                {
                                    var didPush = PushToRemote(repository,
                                                               GitReference.CreateFromString(applicationSource.TargetRevision),
                                                               deploymentConfig.CommitParameters,
                                                               updatedFiles,
                                                               updatedImages);

                                    if (didPush)
                                    {
                                        newImagesWritten.UnionWith(updatedImages);
                                        updatedApplicationsWithSources.GetOrAdd(applicationFromYaml.Metadata.Name.ToApplicationName(), _ => new HashSet<ApplicationSourceName?>()).Add(applicationSource.Name.ToApplicationSourceName());
                                        gitReposUpdated.Add(applicationSource.RepoUrl.AbsoluteUri);
                                    }
                                    
                                    return didPush;
                                }
                            }
                        }

                        return false;
                    }
                    case SourceType.Helm:
                    {
                        return WorkOnHelm(applicationFromYaml, application, applicationSource, containsMultipleSources,
                                   deploymentScope,
                                   gitCredentials,
                                   repositoryFactory,
                                   deploymentConfig,
                                   newImagesWritten,
                                   updatedApplicationsWithSources,
                                   gitReposUpdated);
                    }
                    case SourceType.Kustomize:
                    {
                        using (var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory))
                        {
                            var repoSubPath = Path.Combine(repository.WorkingDirectory, applicationSource.Path!);
                            log.Verbose($"Reading files from {applicationSource.Path}");
                            
                            var (updatedFiles, updatedImages) = UpdateKubernetesYaml(repository.WorkingDirectory, applicationSource.Path!, application.DefaultRegistry, deploymentConfig.ImageReferences);
                            if (updatedImages.Count > 0)
                            {
                                var didPush = PushToRemote(repository,
                                                           GitReference.CreateFromString(applicationSource.TargetRevision),
                                                           deploymentConfig.CommitParameters,
                                                           updatedFiles,
                                                           updatedImages);

                                if (didPush)
                                {
                                    newImagesWritten.UnionWith(updatedImages);
                                    updatedApplicationsWithSources.GetOrAdd(applicationFromYaml.Metadata.Name.ToApplicationName(), _ => new HashSet<ApplicationSourceName?>()).Add(applicationSource.Name.ToApplicationSourceName());
                                    gitReposUpdated.Add(applicationSource.RepoUrl.AbsoluteUri);
                                }
                                
                                return didPush;
                            }
                        }

                        return false;
                    }
                    case SourceType.Plugin:
                    {
                        log.Warn("Can't deal with Plugin");
                        return false;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // if (applicationSource.Path == null)
                // {
                //     log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceIdentity);
                //     return false;
                // }
            }

            return false;
        }

        bool WorkOnHelm(Application applicationFromYaml,
                        ArgoCDApplicationDto application,
                        ApplicationSource applicationSource,
                        bool containsMultipleSources,
                        (ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant) deploymentScope,
                        Dictionary<string, GitCredentialDto> gitCredentials,
                        RepositoryFactory repositoryFactory,
                        UpdateArgoCDAppDeploymentConfig deploymentConfig,
                        HashSet<string> newImagesWritten,
                        Dictionary<ApplicationName, HashSet<ApplicationSourceName?>> updatedApplicationsWithSources,
                        HashSet<string> gitReposUpdated)
        {
            bool didUpdateSomething = false;

            var valuesFilesToUpdate = new List<HelmValuesFileImageUpdateTarget>();
            
            //Add implicit sources
            using (var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory))
            {
                var chartFile = HelmDiscovery.TryFindHelmChartFile(fileSystem, Path.Combine(repository.WorkingDirectory, applicationSource.Path!));
                if (chartFile != null)
                {
                    var repoSubPath = Path.Combine(repository.WorkingDirectory, applicationSource.Path!);

                    HandleAsHelmChart(applicationFromYaml,
                                      application,
                                      applicationSource,
                                      valuesFilesToUpdate,
                                      repoSubPath);
                }
            }
            
            var explicitHelmSources = new HelmValuesFileUpdateTargetParser(applicationFromYaml, application.DefaultRegistry).GetValuesFilesToUpdate(applicationSource);
            LogHelmSourceConfigurationProblems(explicitHelmSources.Problems, applicationFromYaml.Metadata.Annotations, containsMultipleSources, deploymentScope);

            valuesFilesToUpdate.AddRange(explicitHelmSources.Targets);
            foreach (var valuesFileSource in valuesFilesToUpdate)
            {
                didUpdateSomething |= WorkOnHelmUpdateTarget(applicationFromYaml, gitCredentials, repositoryFactory, deploymentConfig,
                                                            newImagesWritten,
                                                            updatedApplicationsWithSources,
                                                            gitReposUpdated,
                                                            valuesFileSource);
            }

          

            return didUpdateSomething;
        }

        bool WorkOnHelmUpdateTarget(Application applicationFromYaml,
                                    Dictionary<string, GitCredentialDto> gitCredentials,
                                    RepositoryFactory repositoryFactory,
                                    UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                    HashSet<string> newImagesWritten,
                                    Dictionary<ApplicationName, HashSet<ApplicationSourceName?>> updatedApplicationsWithSources,
                                    HashSet<string> gitReposUpdated,
                                    HelmValuesFileImageUpdateTarget valuesFileSource)
        {
            var sourceBase = new ApplicationSource()
            {
                RepoUrl = valuesFileSource.RepoUrl,
                TargetRevision = valuesFileSource.TargetRevision,
            };
            using (var repository = CreateRepository(gitCredentials, sourceBase, repositoryFactory))
            {
                var helmUpdateResult = UpdateHelmImageValues(repository.WorkingDirectory,
                                                             valuesFileSource,
                                                             deploymentConfig.ImageReferences
                                                            );
                if (helmUpdateResult.ImagesUpdated.Count > 0)
                {
                    var didPush = PushToRemote(repository, GitReference.CreateFromString(valuesFileSource.TargetRevision),
                                               deploymentConfig.CommitParameters,
                                               new HashSet<string>() { Path.Combine(valuesFileSource.Path, valuesFileSource.FileName) },
                                               helmUpdateResult.ImagesUpdated);

                    if (didPush)
                    {
                        newImagesWritten.UnionWith(helmUpdateResult.ImagesUpdated);
                        updatedApplicationsWithSources.GetOrAdd(applicationFromYaml.Metadata.Name.ToApplicationName(), _ => new HashSet<ApplicationSourceName?>()).Add(valuesFileSource.SourceName);
                        gitReposUpdated.Add(valuesFileSource.RepoUrl.ToString());
                    }

                    return didPush;
                }
            }

            return false;
        }

        void LogHelmSourceConfigurationProblems(IReadOnlyCollection<HelmSourceConfigurationProblem> helmSourceConfigurationProblems,
                                                IReadOnlyDictionary<string, string> annotations,
                                                bool containsMultipleSources,
                                                (ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant) deploymentScope)
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
                        var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(helmSourceIsMissingImagePathAnnotation.ScopingSourceName, annotations, containsMultipleSources);
                        if (annotatedScope == deploymentScope)
                        {
                            log.WarnFormat("The Helm source '{0}' is missing an annotation for the image replace path. It will not be updated.",
                                           helmSourceIsMissingImagePathAnnotation.HelmSourceRepoUrl.AbsoluteUri);
                        }

                        return;
                    }
                    case RefSourceIsMissing refSourceIsMissing:
                    {
                        log.WarnFormat("A source referenced by Helm source '{0}' is missing: {1}", 
                                       refSourceIsMissing.HelmSourceRepoUrl.AbsoluteUri, refSourceIsMissing.Ref);
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

        void HandleAsHelmChart(Application applicationFromYaml,
                               ArgoCDApplicationDto application,
                               ApplicationSource applicationSource,
                               List<HelmValuesFileImageUpdateTarget> valuesFilesToUpdate,
                               string repoSubPath)
        {
            var imageReplacePaths = ScopingAnnotationReader.GetImageReplacePathsForApplicationSource(
                                                                                                               applicationSource.Name.ToApplicationSourceName(), 
                                                                                                               applicationFromYaml.Metadata.Annotations, 
                                                                                                               applicationFromYaml.Spec.Sources.Count > 1);
            if (!imageReplacePaths.Any())
            {
                GenerateHelmAnnotationLogMessages(applicationFromYaml, repoSubPath);
            }
            else
            {
                log.Info($"Application '{application.Name}' source at `{applicationSource.RepoUrl.AbsoluteUri}' is a helm chart, its values file will be subsequently updated.");
                valuesFilesToUpdate.Add(new HelmValuesFileImageUpdateTarget(
                                                                            applicationFromYaml.Metadata.Name.ToApplicationName(),
                                                                            applicationSource.Name.ToApplicationSourceName(),
                                                                            application.DefaultRegistry,
                                                                            applicationSource.Path,
                                                                            applicationSource.RepoUrl,
                                                                            applicationSource.TargetRevision,
                                                                            HelmDiscovery.TryFindValuesFile(fileSystem, repoSubPath),
                                                                            imageReplacePaths));
            }
        }

        (HashSet<string>, HashSet<string>) UpdateKubernetesYaml(string rootPath,
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
            }
            else
            {
                filesToUpdate = FindYamlFiles(absSubFolder).ToHashSet();
                imageReplacerFactory = yaml => new ContainerImageReplacer(yaml, defaultRegistry);
                log.Verbose($"Found {filesToUpdate.Count} yaml files to process");
            }

            return Update(rootPath, imagesToUpdate, filesToUpdate, imageReplacerFactory);
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
                    return new HelmRefUpdatedResult(target.RepoUrl, imageUpdateResult.UpdatedImageReferences);
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to commit changes to the Git Repository: {ex.Message}");
                    throw;
                }
            }

            return new HelmRefUpdatedResult(target.RepoUrl, new HashSet<string>());
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

        void GenerateHelmAnnotationLogMessages(Application app, string subPath)
        {
            log.WarnFormat("Argo CD Application '{0}' contains a helm chart ({1}), however the application is missing Octopus-specific annotations required for image-tag updating in Helm.",
                           app.Metadata.Name,
                           Path.Combine(subPath, ArgoCDConstants.HelmChartFileName));
            log.WarnFormat("Annotation creation documentation can be found {0}.", log.FormatShortLink("argo-cd-helm-image-annotations", "here"));
        }

        //NOTE: rootPath needs to include the subfolder
        IEnumerable<string> FindYamlFiles(string rootPath)
        {
            var yamlFileGlob = "**/*.{yaml,yml}";
            return fileSystem.EnumerateFilesWithGlob(rootPath, yamlFileGlob);
        }
    }
}

#endif

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
        readonly IArgoCDFilesUpdatedReporter reporter;
        readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

        public UpdateArgoCDAppImagesInstallConvention(
            ILog log,
            ICalamariFileSystem fileSystem,
            DeploymentConfigFactory deploymentConfigFactory,
            ICommitMessageGenerator commitMessageGenerator,
            ICustomPropertiesLoader customPropertiesLoader,
            IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
            IGitVendorAgnosticApiAdapterFactory gitVendorAgnosticApiAdapterFactory,
            IClock clock,
            IArgoCDFilesUpdatedReporter reporter,
            ArgoCDOutputVariablesWriter outputVariablesWriter)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.deploymentConfigFactory = deploymentConfigFactory;
            this.commitMessageGenerator = commitMessageGenerator;
            this.customPropertiesLoader = customPropertiesLoader;
            this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
            this.gitVendorAgnosticApiAdapterFactory = gitVendorAgnosticApiAdapterFactory;
            this.clock = clock;
            this.reporter = reporter;
            this.outputVariablesWriter = outputVariablesWriter;
        }

        public void Install(RunningDeployment deployment)
        {
            log.Verbose("Executing Update Argo CD Application Images");
            var deploymentConfig = deploymentConfigFactory.CreateUpdateImageConfig(deployment);

            var repositoryFactory = new RepositoryFactory(log,
                                                          fileSystem,
                                                          deployment.CurrentDirectory,
                                                          gitVendorAgnosticApiAdapterFactory,
                                                          clock);

            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();

            var gitCredentials = argoProperties.Credentials.ToDictionary(c => c.Url);
            var deploymentScope = deployment.Variables.GetDeploymentScope();

            log.LogApplicationCounts(deploymentScope, argoProperties.Applications);

            var applicationResults = argoProperties.Applications
                                                   .Select(application =>
                                                           {
                                                               var gateway = argoProperties.Gateways.Single(g => g.Id == application.GatewayId);
                                                               return ProcessApplication(application,
                                                                                         gateway,
                                                                                         deploymentScope,
                                                                                         gitCredentials,
                                                                                         repositoryFactory,
                                                                                         deploymentConfig);
                                                           })
                                                   .ToList();

            reporter.ReportFilesUpdated(applicationResults);

            var totalApplicationsWithSourceCounts = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApplications = applicationResults.Where(r => r.Updated).ToList();
            var updatedApplicationsWithSources = updatedApplications.Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();
            var gitReposUpdated = updatedApplications.SelectMany(r => r.GitReposUpdated).ToHashSet();
            var newImagesWritten = updatedApplications.SelectMany(r => r.UpdatedImages).ToHashSet();

            var gatewayIds = argoProperties.Applications.Select(a => a.GatewayId).ToHashSet();
            outputVariablesWriter.WriteImageUpdateOutput(gatewayIds,
                                                         gitReposUpdated,
                                                         totalApplicationsWithSourceCounts,
                                                         updatedApplicationsWithSources,
                                                         newImagesWritten.Count
                                                        );
        }

        ProcessApplicationResult ProcessApplication(
            ArgoCDApplicationDto application,
            ArgoCDGatewayDto gateway,
            DeploymentScope deploymentScope,
            Dictionary<string, GitCredentialDto> gitCredentials,
            RepositoryFactory repositoryFactory,
            UpdateArgoCDAppDeploymentConfig deploymentConfig)
        {
            log.InfoFormat("Processing application {0}", application.Name);
            var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);
            var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;
            var applicationName = applicationFromYaml.Metadata.Name;

            ValidateApplication(applicationFromYaml);

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
                                                                                       application.DefaultRegistry,
                                                                                       gateway),
                                                               applicationSource,
                                                           })
                                                           .Where(r => r.Updated.ImagesUpdated.Any())
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

            return new ProcessApplicationResult(
                                                application.GatewayId,
                                                applicationName.ToApplicationName(),
                                                applicationFromYaml.Spec.Sources.Count,
                                                applicationFromYaml.Spec.Sources.Count(s => deploymentScope.Matches(ScopingAnnotationReader.GetScopeForApplicationSource(s.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources))),
                                                updatedSourcesResults.Select(r => new UpdatedSourceDetail(r.Updated.CommitSha, r.applicationSource.Index, [], r.Updated.PatchedFiles)).ToList(),
                                                updatedSourcesResults.SelectMany(r => r.Updated.ImagesUpdated).ToHashSet(),
                                                updatedSourcesResults.Select(r => r.applicationSource.Source.OriginalRepoUrl).ToHashSet());
        }

        void ValidateApplication(Application applicationFromYaml)
        {
            var validationResult = ValidationResult.Merge(
                                                          ApplicationValidator.ValidateSourceNames(applicationFromYaml),
                                                          ApplicationValidator.ValidateUnnamedAnnotationsInMultiSourceApplication(applicationFromYaml),
                                                          ApplicationValidator.ValidateSourceTypes(applicationFromYaml)
                                                         );
            validationResult.Action(log);
        }

        SourceUpdateResult ProcessSource(
            ApplicationSourceWithMetadata sourceWithMetadata,
            Application applicationFromYaml,
            bool containsMultipleSources,
            DeploymentScope deploymentScope,
            Dictionary<string, GitCredentialDto> gitCredentials,
            RepositoryFactory repositoryFactory,
            UpdateArgoCDAppDeploymentConfig deploymentConfig,
            string defaultRegistry,
            ArgoCDGatewayDto gateway)
        {
            var applicationSource = sourceWithMetadata.Source;
            var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources);

            log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);
            if (!deploymentScope.Matches(annotatedScope))
            {
                return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
            }

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
                                     defaultRegistry,
                                     gateway)
                        : ProcessDirectory(applicationFromYaml,
                                           gitCredentials,
                                           repositoryFactory,
                                           deploymentConfig,
                                           sourceWithMetadata,
                                           defaultRegistry,
                                           gateway);
                }
                case SourceType.Helm:
                {
                    return ProcessHelm(applicationFromYaml,
                                       sourceWithMetadata,
                                       gitCredentials,
                                       repositoryFactory,
                                       deploymentConfig,
                                       defaultRegistry,
                                       gateway);
                }
                case SourceType.Kustomize:
                {
                    return ProcessKustomize(applicationFromYaml,
                                            gitCredentials,
                                            repositoryFactory,
                                            deploymentConfig,
                                            sourceWithMetadata,
                                            defaultRegistry,
                                            gateway);
                }
                case SourceType.Plugin:
                {
                    log.WarnFormat("Unable to update source '{0}' as Plugin sources aren't currently supported.", sourceWithMetadata.SourceIdentity);
                    return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <returns>Images that were updated</returns>
        SourceUpdateResult ProcessKustomize(
            Application applicationFromYaml,
            Dictionary<string, GitCredentialDto> gitCredentials,
            RepositoryFactory repositoryFactory,
            UpdateArgoCDAppDeploymentConfig deploymentConfig,
            ApplicationSourceWithMetadata sourceWithMetadata,
            string defaultRegistry,
            ArgoCDGatewayDto gateway)
        {
            var applicationSource = sourceWithMetadata.Source;

            if (applicationSource.Path == null)
            {
                log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
                return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
            }

            using (var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory))
            {
                log.Verbose($"Reading files from {applicationSource.Path}");

                var (updatedFiles, updatedImages, patchedFiles) = UpdateKustomizeYaml(repository.WorkingDirectory, applicationSource.Path!, defaultRegistry, deploymentConfig.PackageWithHelmReference.Select(ph => ph.ContainerReference).ToList());
                if (updatedImages.Count > 0)
                {
                    var pushResult = PushToRemote(repository,
                                                  GitReference.CreateFromString(applicationSource.TargetRevision),
                                                  deploymentConfig.CommitParameters,
                                                  updatedFiles,
                                                  updatedImages);

                    if (pushResult is not null)
                    {
                        outputVariablesWriter.WritePushResultOutput(gateway.Name,
                                                                    applicationFromYaml.Metadata.Name,
                                                                    sourceWithMetadata.Index,
                                                                    pushResult);
                        return new SourceUpdateResult(updatedImages, pushResult.CommitSha, patchedFiles);
                    }
                }
            }

            return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
        }

        /// <returns>Images that were updated</returns>
        SourceUpdateResult ProcessRef(
            Application applicationFromYaml,
            Dictionary<string, GitCredentialDto> gitCredentials,
            RepositoryFactory repositoryFactory,
            UpdateArgoCDAppDeploymentConfig deploymentConfig,
            ApplicationSourceWithMetadata sourceWithMetadata,
            string defaultRegistry,
            ArgoCDGatewayDto gateway)
        {
            var applicationSource = sourceWithMetadata.Source;

            if (applicationSource.Path != null)
            {
                log.WarnFormat("The source '{0}' contains a Ref, only referenced files will be updated. Please create another source with the same URL if you wish to update files under the path.", sourceWithMetadata.SourceIdentity);
            }

            if (!deploymentConfig.UseHelmValueYamlPathFromStep)
            {
                var helmTargetsForRefSource = new HelmValuesFileUpdateTargetParser(applicationFromYaml, defaultRegistry)
                    .GetHelmTargetsForRefSource(sourceWithMetadata);

                LogHelmSourceConfigurationProblems(helmTargetsForRefSource.Problems);

                using var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory);
                return ProcessHelmUpdateTargets(
                                                applicationFromYaml,
                                                repository,
                                                deploymentConfig,
                                                sourceWithMetadata,
                                                helmTargetsForRefSource.Targets,
                                                gateway);
            }

            return ProcessRefSourceUsingStepVariables(applicationFromYaml,
                                                      sourceWithMetadata,
                                                      gitCredentials,
                                                      repositoryFactory,
                                                      deploymentConfig,
                                                      defaultRegistry,
                                                      gateway);
        }

        /// <returns>Images that were updated</returns>
        SourceUpdateResult ProcessDirectory(
            Application applicationFromYaml,
            Dictionary<string, GitCredentialDto> gitCredentials,
            RepositoryFactory repositoryFactory,
            UpdateArgoCDAppDeploymentConfig deploymentConfig,
            ApplicationSourceWithMetadata sourceWithMetadata,
            string defaultRegistry,
            ArgoCDGatewayDto gateway)
        {
            var applicationSource = sourceWithMetadata.Source;
            if (applicationSource.Path == null)
            {
                log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
                return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
            }

            using (var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory))
            {
                log.Verbose($"Reading files from {applicationSource.Path}");

                var (updatedFiles, updatedImages, patchedFiles) = UpdateKubernetesYaml(repository.WorkingDirectory, applicationSource.Path!, defaultRegistry, deploymentConfig.PackageWithHelmReference.Select(ph => ph.ContainerReference).ToList());
                if (updatedImages.Count > 0)
                {
                    var pushResult = PushToRemote(repository,
                                                  GitReference.CreateFromString(applicationSource.TargetRevision),
                                                  deploymentConfig.CommitParameters,
                                                  updatedFiles,
                                                  updatedImages);

                    if (pushResult is not null)
                    {
                        outputVariablesWriter.WritePushResultOutput(gateway.Name,
                                                                    applicationFromYaml.Metadata.Name,
                                                                    sourceWithMetadata.Index,
                                                                    pushResult);
                        return new SourceUpdateResult(updatedImages, pushResult.CommitSha, patchedFiles);
                    }

                    return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
                }
            }

            return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
        }

        /// <returns>Images that were updated</returns>
        SourceUpdateResult ProcessHelm(
            Application applicationFromYaml,
            ApplicationSourceWithMetadata sourceWithMetadata,
            Dictionary<string, GitCredentialDto> gitCredentials,
            RepositoryFactory repositoryFactory,
            UpdateArgoCDAppDeploymentConfig deploymentConfig,
            string defaultRegistry,
            ArgoCDGatewayDto gateway)
        {
            var applicationSource = sourceWithMetadata.Source;

            if (applicationSource.Path == null)
            {
                log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
                return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
            }

            if (!deploymentConfig.UseHelmValueYamlPathFromStep)
            {
                return ProcessHelmSourceUsingAnnotations(applicationFromYaml,
                                                         sourceWithMetadata,
                                                         gitCredentials,
                                                         repositoryFactory,
                                                         deploymentConfig,
                                                         defaultRegistry,
                                                         gateway,
                                                         applicationSource);
            }

            return ProcessHelmSourceUsingStepVariables(applicationFromYaml,
                                                       sourceWithMetadata,
                                                       gitCredentials,
                                                       repositoryFactory,
                                                       deploymentConfig,
                                                       defaultRegistry,
                                                       gateway,
                                                       applicationSource);
        }

        SourceUpdateResult ProcessHelmSourceUsingAnnotations(Application applicationFromYaml,
                                                             ApplicationSourceWithMetadata sourceWithMetadata,
                                                             Dictionary<string, GitCredentialDto> gitCredentials,
                                                             RepositoryFactory repositoryFactory,
                                                             UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                                             string defaultRegistry,
                                                             ArgoCDGatewayDto gateway,
                                                             ApplicationSource applicationSource)
        {
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

            return ProcessHelmUpdateTargets(applicationFromYaml,
                                            repository,
                                            deploymentConfig,
                                            sourceWithMetadata,
                                            valuesFilesToUpdate,
                                            gateway);
        }

        /// <returns>Images that were updated</returns>
        SourceUpdateResult ProcessHelmUpdateTargets(
            Application applicationFromYaml,
            RepositoryWrapper repository,
            UpdateArgoCDAppDeploymentConfig deploymentConfig,
            ApplicationSourceWithMetadata sourceWithMetadata,
            IReadOnlyCollection<HelmValuesFileImageUpdateTarget> targets,
            ArgoCDGatewayDto gateway)
        {
            var results = targets.Select(t => UpdateHelmImageValues(repository.WorkingDirectory,
                                                                    t,
                                                                    deploymentConfig.PackageWithHelmReference.Select(ph => ph.ContainerReference).ToList()
                                                                   ))
                                 .ToList();

            var updatedImages = results.SelectMany(r => r.ImagesUpdated).ToHashSet();
            if (updatedImages.Count > 0)
            {
                var patchedFiles = results.Select(r => new FilePathContent(r.RelativeFilepath, r.JsonPatch)).ToList();

                var pushResult = PushToRemote(repository,
                                              GitReference.CreateFromString(sourceWithMetadata.Source.TargetRevision),
                                              deploymentConfig.CommitParameters,
                                              results.Where(r => r.ImagesUpdated.Any()).Select(r => r.RelativeFilepath).ToHashSet(),
                                              updatedImages);

                if (pushResult is not null)
                {
                    outputVariablesWriter.WritePushResultOutput(gateway.Name,
                                                                applicationFromYaml.Metadata.Name,
                                                                sourceWithMetadata.Index,
                                                                pushResult);
                    return new SourceUpdateResult(updatedImages, pushResult.CommitSha, patchedFiles);
                }
            }

            return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
        }

        SourceUpdateResult ProcessHelmSourceUsingStepVariables(Application applicationFromYaml,
                                                               ApplicationSourceWithMetadata sourceWithMetadata,
                                                               Dictionary<string, GitCredentialDto> gitCredentials,
                                                               RepositoryFactory repositoryFactory,
                                                               UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                                               string defaultRegistry,
                                                               ArgoCDGatewayDto gateway,
                                                               ApplicationSource applicationSource)
        {
            var extractor = new HelmValuesFileExtractor(applicationFromYaml, defaultRegistry);
            var valuesFilesInHelmSource = extractor.GetInlineValuesFilesReferencedByHelmSource(sourceWithMetadata);

            //Add the implicit value file if needed
            using var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory);
            var implicitValuesFile = HelmDiscovery.TryFindValuesFile(fileSystem, sourceWithMetadata.Source.Path!);
            var valuesFilesToUpdate = valuesFilesInHelmSource.ToList();
            if (implicitValuesFile != null && !valuesFilesInHelmSource.Contains(implicitValuesFile))
            {
                valuesFilesToUpdate.Add(implicitValuesFile);
            }

            return UpdateImageTagsInValuesFiles(valuesFilesToUpdate,
                                                deploymentConfig.PackageWithHelmReference,
                                                defaultRegistry,
                                                deploymentConfig,
                                                repository,
                                                sourceWithMetadata);
        }

        SourceUpdateResult ProcessRefSourceUsingStepVariables(Application applicationFromYaml,
                                                              ApplicationSourceWithMetadata sourceWithMetadata,
                                                              Dictionary<string, GitCredentialDto> gitCredentials,
                                                              RepositoryFactory repositoryFactory,
                                                              UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                                              string defaultRegistry,
                                                              ArgoCDGatewayDto gateway)
        {
            var extractor = new HelmValuesFileExtractor(applicationFromYaml, defaultRegistry);
            var valuesFilesInHelmSource = extractor.GetValueFilesReferencedInRefSource(sourceWithMetadata);

            using var repository = CreateRepository(gitCredentials, sourceWithMetadata.Source, repositoryFactory);
            return UpdateImageTagsInValuesFiles(valuesFilesInHelmSource,
                                                deploymentConfig.PackageWithHelmReference,
                                                defaultRegistry,
                                                deploymentConfig,
                                                repository,
                                                sourceWithMetadata);
        }

        SourceUpdateResult UpdateImageTagsInValuesFiles(IReadOnlyCollection<string> valuesFilesToUpdate,
                                                        IReadOnlyCollection<PackageAndHelmReference> stepReferencedContainers,
                                                        string defaultRegistry,
                                                        UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                                        RepositoryWrapper repository,
                                                        ApplicationSourceWithMetadata sourceWithMetadata)
        {
            var filesUpdated = new HashSet<string>();
            var imagesUpdated = new HashSet<string>();
            foreach (var valuesFile in valuesFilesToUpdate)
            {
                var wasUpdated = false;
                var repoRelativePath = Path.Combine(repository.WorkingDirectory, valuesFile);
                log.Info($"Processing file at {valuesFile}.");
                var fileContent = fileSystem.ReadFile(repoRelativePath);
                var originalYamlParser = new HelmYamlParser(fileContent); // Parse and track the original yaml so that content can be read from it.
                var flattenedYamlPathDictionary = HelmValuesEditor.CreateFlattenedDictionary(originalYamlParser);
                foreach (var container in stepReferencedContainers.Where(c => c.HelmReference is not null))
                {
                    if (flattenedYamlPathDictionary.TryGetValue(container.HelmReference!, out var valueToUpdate))
                    {
                        if (IsUnstructuredText(valueToUpdate))
                        {
                            HelmValuesEditor.UpdateNodeValue(fileContent, container.HelmReference!, container.ContainerReference.Tag);
                            filesUpdated.Add(valuesFile);
                            imagesUpdated.Add(container.ContainerReference.ToString());
                        }
                        else
                        {
                            var cir = ContainerImageReference.FromReferenceString(valueToUpdate, defaultRegistry);
                            var comparison = container.ContainerReference.CompareWith(cir);
                            if (comparison.MatchesImage())
                            {
                                if (!comparison.TagMatch)
                                {
                                    var newValue = cir.WithTag(container.ContainerReference.Tag);
                                    fileContent = HelmValuesEditor.UpdateNodeValue(fileContent, container.HelmReference!, newValue);
                                    wasUpdated = true;
                                    filesUpdated.Add(valuesFile);
                                    imagesUpdated.Add(newValue);
                                }
                            }
                            else
                            {
                                log.Warn($"Attempted to update value entry '{container.HelmReference}', however it contains a mismatched image name and registry.");
                            }
                        }

                        if (wasUpdated)
                        {
                            fileSystem.WriteAllText(repoRelativePath, fileContent);
                        }
                    }
                }

                PushToRemote(repository,
                             GitReference.CreateFromString(sourceWithMetadata.Source.TargetRevision), //this is a hack, shouldn't need to KEEP re-converting it.
                             deploymentConfig.CommitParameters,
                             filesUpdated,
                             imagesUpdated);
            }

            return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
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
            var gitCredential = gitCredentials.GetValueOrDefault(source.OriginalRepoUrl);
            if (gitCredential == null)
            {
                log.Info($"No Git credentials found for: '{source.OriginalRepoUrl}', will attempt to clone repository anonymously.");
            }

            var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, source.CloneSafeRepoUrl, GitReference.CreateFromString(source.TargetRevision));
            return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
        }

        (HelmValuesFileImageUpdateTarget? Target, HelmSourceConfigurationProblem? Problem) AddImplicitValuesFile(
            Application applicationFromYaml,
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

            return (new HelmValuesFileImageUpdateTarget(defaultRegistry,
                                                        applicationSource.Source.Path,
                                                        valuesFilename,
                                                        imageReplacePaths), null);
        }

        (HashSet<string>, HashSet<string>, List<FilePathContent>) UpdateKubernetesYaml(
            string rootPath,
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

        (HashSet<string>, HashSet<string>, List<FilePathContent>) UpdateKustomizeYaml(
            string rootPath,
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
            return ([], [], []);
        }

        (HashSet<string>, HashSet<string>, List<FilePathContent>) Update(string rootPath, List<ContainerImageReference> imagesToUpdate, HashSet<string> filesToUpdate, Func<string, IContainerImageReplacer> imageReplacerFactory)
        {
            var updatedFiles = new HashSet<string>();
            var updatedImages = new HashSet<string>();
            var jsonPatches = new List<FilePathContent>();
            foreach (var file in filesToUpdate)
            {
                var relativePath = Path.GetRelativePath(rootPath, file);
                log.Verbose($"Processing file {relativePath}.");
                var content = fileSystem.ReadFile(file);

                var imageReplacer = imageReplacerFactory(content);
                var imageReplacementResult = imageReplacer.UpdateImages(imagesToUpdate);

                // TODO: Generate JSON patch from changes and add to jsonPatches

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

            return (updatedFiles, updatedImages, jsonPatches);
        }
        

        HelmRefUpdatedResult UpdateHelmImageValues(
            string rootPath,
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
                    // TODO: Fill in JSON patch
                    return new HelmRefUpdatedResult(imageUpdateResult.UpdatedImageReferences, Path.Combine(target.Path, target.FileName), "");
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to commit changes to the Git Repository: {ex.Message}");
                    throw;
                }
            }

            return new HelmRefUpdatedResult(new HashSet<string>(), Path.Combine(target.Path, target.FileName), string.Empty);
        }

        PushResult? PushToRemote(
            RepositoryWrapper repository,
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
                return null;

            log.Verbose("Pushing to remote");
            return repository.PushChanges(commitParameters.RequiresPr,
                                          commitParameters.Summary,
                                          commitDescription,
                                          branchName,
                                          CancellationToken.None)
                             .GetAwaiter()
                             .GetResult();
        }

        //NOTE: rootPath needs to include the subfolder
        IEnumerable<string> FindYamlFiles(string rootPath)
        {
            var yamlFileGlob = "**/*.{yaml,yml}";
            return fileSystem.EnumerateFilesWithGlob(rootPath, yamlFileGlob);
        }

        record SourceUpdateResult(HashSet<string> ImagesUpdated, string CommitSha, List<FilePathContent> PatchedFiles);
    }
}
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
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Microsoft.IdentityModel.Tokens;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppImagesInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly IGitHubPullRequestCreator pullRequestCreator;
        readonly DeploymentConfigFactory deploymentConfigFactory;
        readonly ICommitMessageGenerator commitMessageGenerator;
        readonly ICustomPropertiesLoader customPropertiesLoader;
        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser;
        int repositoryNumber = 1;

        public UpdateArgoCDAppImagesInstallConvention(ILog log,
                                                      IGitHubPullRequestCreator pullRequestCreator,
                                                      ICalamariFileSystem fileSystem,
                                                      DeploymentConfigFactory deploymentConfigFactory,
                                                      ICommitMessageGenerator commitMessageGenerator,
                                                      ICustomPropertiesLoader customPropertiesLoader,
                                                      IArgoCDApplicationManifestParser argoCdApplicationManifestParser)
        {
            this.log = log;
            this.pullRequestCreator = pullRequestCreator;
            this.fileSystem = fileSystem;
            this.deploymentConfigFactory = deploymentConfigFactory;
            this.commitMessageGenerator = commitMessageGenerator;
            this.customPropertiesLoader = customPropertiesLoader;
            this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
        }

        public void Install(RunningDeployment deployment)
        {
            Log.Info("Executing Update Argo CD Application Images");
            var deploymentConfig = deploymentConfigFactory.CreateUpdateImageConfig(deployment);

            var repositoryFactory = new RepositoryFactory(log, deployment.CurrentDirectory, pullRequestCreator);

            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();

            var gitCredentials = argoProperties.Credentials.ToDictionary(c => c.Url);
            var deploymentScope = deployment.Variables.GetDeploymentScope();

            log.Info($"Found {argoProperties.Applications.Length} Argo CD apps to update");
            var updatedApplications = new List<string>();
            var newImagesWritten = new HashSet<string>();
            var gitReposUpdated = new HashSet<string>();
            var gatewayIds = new HashSet<string>();

            foreach (var application in argoProperties.Applications)
            {
                log.InfoFormat("Processing application {0}", application.Name);

                var instanceLinks = application.InstanceWebUiUrl != null ? new ArgoCDInstanceLinks(application.InstanceWebUiUrl) : null;

                var valuesFilesToUpdate = new List<HelmValuesFileImageUpdateTarget>();
                var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);

                ApplicationSourceValidator.ValidateApplicationSources(applicationFromYaml);
                log.LogUnnamedAnnotationsInMultiSourceApplication(applicationFromYaml);
                
                gatewayIds.Add(application.GatewayId);
                bool containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;

                var didUpdateSomething = false;
                foreach (var applicationSource in applicationFromYaml.Spec.Sources.OfType<BasicSource>())
                {
                    var repository = CreateRepository(gitCredentials, applicationSource, repositoryFactory);
                    var repoSubPath = Path.Combine(repository.WorkingDirectory, applicationSource.Path);

                    var chartFile = HelmDiscovery.TryFindHelmChartFile(fileSystem, Path.Combine(repository.WorkingDirectory, applicationSource.Path));
                    if (chartFile != null)
                    {
                        HandleAsHelmChart(deployment,
                                          applicationFromYaml,
                                          application,
                                          applicationSource,
                                          valuesFilesToUpdate,
                                          repoSubPath);
                        continue;
                    }

                    var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources);
                    log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);

                    if (annotatedScope == deploymentScope)
                    {
                        var (updatedFiles, updatedImages) = UpdateKubernetesYaml(repository.WorkingDirectory, applicationSource.Path, application.DefaultRegistry, deploymentConfig.ImageReferences);
                        if (updatedImages.Count > 0)
                        {
                            var didPush = PushToRemote(repository,
                                                       new GitBranchName(applicationSource.TargetRevision),
                                                       deploymentConfig.CommitParameters,
                                                       updatedFiles,
                                                       updatedImages);

                            didUpdateSomething |= didPush;

                            newImagesWritten.UnionWith(updatedImages);
                            updatedApplications.Add(applicationFromYaml.Metadata.Name);
                            gitReposUpdated.Add(applicationSource.RepoUrl.AbsoluteUri);
                        }
                    }
                }

                var explicitHelmSources = new HelmValuesFileUpdateTargetParser(applicationFromYaml, application.DefaultRegistry).GetValuesFilesToUpdate();
                valuesFilesToUpdate.AddRange(explicitHelmSources);
                foreach (var valuesFileSource in valuesFilesToUpdate)
                {
                    if (valuesFileSource is InvalidHelmValuesFileImageUpdateTarget invalidSource)
                    {
                        log.Warn($"Invalid annotations setup detected.\nAlias defined: {invalidSource.Alias}. Missing corresponding {ArgoCDConstants.Annotations.OctopusImageReplacementPathsKeyWithSpecifier(invalidSource.Alias)} annotation.");
                        continue;
                    }
                    
                    var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(valuesFileSource.SourceName, applicationFromYaml.Metadata.Annotations, containsMultipleSources);
                    log.LogApplicationSourceScopeStatus(annotatedScope, valuesFileSource.SourceName, deploymentScope);

                    if (annotatedScope == deploymentScope)
                    {
                        var sourceBase = new SourceBase()
                        {
                            RepoUrl = valuesFileSource.RepoUrl,
                            TargetRevision = valuesFileSource.TargetRevision,
                        };
                        var repository = CreateRepository(gitCredentials, sourceBase, repositoryFactory);

                        var helmUpdateResult = UpdateHelmImageValues(repository.WorkingDirectory,
                                                                     valuesFileSource,
                                                                     deploymentConfig.ImageReferences
                                                                    );
                        if (helmUpdateResult.ImagesUpdated.Count > 0)
                        {
                            var didPush = PushToRemote(repository,
                                                       new GitBranchName(valuesFileSource.TargetRevision),
                                                       deploymentConfig.CommitParameters,
                                                       new HashSet<string>() { Path.Combine(valuesFileSource.Path, valuesFileSource.FileName) },
                                                       helmUpdateResult.ImagesUpdated);

                            didUpdateSomething |= didPush;

                            newImagesWritten.UnionWith(helmUpdateResult.ImagesUpdated);
                            updatedApplications.Add(applicationFromYaml.Metadata.Name);
                            gitReposUpdated.Add(valuesFileSource.RepoUrl.ToString());
                        }
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

            var outputWriter = new ArgoCDImageUpdateOutputWriter(log);
            outputWriter.WriteImageUpdateOutput(gatewayIds,
                                                gitReposUpdated,
                                                argoProperties.Applications.Select(a => a.Name),
                                                updatedApplications.Distinct(),
                                                newImagesWritten.Count
                                               );
        }

        RepositoryWrapper CreateRepository(Dictionary<string, GitCredentialDto> gitCredentials, SourceBase source, RepositoryFactory repositoryFactory)
        {
            var gitCredential = gitCredentials.GetValueOrDefault(source.RepoUrl.AbsoluteUri);
            if (gitCredential == null)
            {
                log.Info($"No Git credentials found for: '{source.RepoUrl.AbsoluteUri}', will attempt to clone repository anonymously.");
            }

            var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, source.RepoUrl.AbsoluteUri, new GitBranchName(source.TargetRevision));
            return repositoryFactory.CloneRepository(repositoryNumber++.ToString(CultureInfo.InvariantCulture), gitConnection);
        }

        void HandleAsHelmChart(RunningDeployment deployment,
                               Application applicationFromYaml,
                               ArgoCDApplicationDto application,
                               BasicSource applicationSource,
                               List<HelmValuesFileImageUpdateTarget> valuesFilesToUpdate,
                               string repoSubPath)
        {
            var imageReplacePathAnnotations = applicationFromYaml.Metadata.Annotations
                                                                 .Where(a => a.Key.StartsWith(ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey))
                                                                 .SelectMany(a => HelmValuesFileUpdateTargetParser.ConvertAnnotationToList(a.Value))
                                                                 .ToList();
            if (imageReplacePathAnnotations.IsNullOrEmpty())
            {
                GenerateHelmAnnotationLogMessages(applicationFromYaml, applicationSource);
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
                                                                            imageReplacePathAnnotations));
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
            }
            else
            {
                filesToUpdate = FindYamlFiles(absSubFolder).ToHashSet();
                imageReplacerFactory = yaml => new ContainerImageReplacer(yaml, defaultRegistry);
            }

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
                    log.Verbose($"Updating file {file} with new image references.");
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
                          GitBranchName branchName,
                          GitCommitParameters commitParameters,
                          HashSet<string> updatedFiles,
                          HashSet<string> updatedImages)
        {
            Log.Info("Staging files in repository");
            repository.StageFiles(updatedFiles.ToArray());

            var commitDescription = commitMessageGenerator.GenerateDescription(updatedImages, commitParameters.Description);

            Log.Info("Commiting changes");
            if (repository.CommitChanges(commitParameters.Summary, commitDescription))
            {
                Log.Info("Changes were commited, pushing to remote");
                repository.PushChanges(commitParameters.RequiresPr,
                                       commitParameters.Summary,
                                       commitDescription,
                                       branchName,
                                       CancellationToken.None)
                          .GetAwaiter()
                          .GetResult();

                return true;
            }

            Log.Info("No changes were commited.");
            return false;
        }

        void GenerateHelmAnnotationLogMessages(Application app, BasicSource source)
        {
            log.WarnFormat("Argo CD Application '{0}' contains a helm chart ({1}), however the application is missing Octopus-specific annotations required for image-tag updating in Helm.",
                           app.Metadata.Name,
                           Path.Combine(source.Path, ArgoCDConstants.HelmChartFileName));
            log.WarnFormat("Annotation creation documentation can be found {0}.", log.FormatShortLink("argo-cd-helm-image-annotations", "here"));
        }

        //NOTE: rootPath needs to include the subfolder
        IEnumerable<string> FindYamlFiles(string rootPath)
        {
            var yamlFileGlob = "**/*.yaml";
            return fileSystem.EnumerateFilesWithGlob(rootPath, yamlFileGlob);
        }
    }
}

#endif
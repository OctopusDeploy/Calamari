#if NET
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Helm;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;

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
            
            log.Info($"Found {argoProperties.Applications.Length} Argo CD apps to update");
            var updatedApplications = new List<string>();
            var newImagesWritten = new HashSet<string>();
            var gitReposUpdated = new HashSet<string>();
            var gatewayIds = new HashSet<string>();
            
            foreach (var application in argoProperties.Applications)
            {
                var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);
                gatewayIds.Add(application.GatewayId);
                
                foreach (var applicationSource in applicationFromYaml.Spec.Sources.OfType<BasicSource>())
                {
                    var gitCredential = gitCredentials[applicationSource.RepoUrl.AbsoluteUri];
                    var gitConnection = new GitConnection(gitCredential.Username, gitCredential.Password, applicationSource.RepoUrl.AbsoluteUri, new GitBranchName(applicationSource.TargetRevision));
                    var repository = repositoryFactory.CloneRepository(repositoryNumber.ToString(CultureInfo.InvariantCulture), gitConnection);

                    var (updatedFiles, updatedImages) = UpdateKubernetesYaml(repository.WorkingDirectory, applicationSource.Path, application.DefaultRegistry, deploymentConfig.ImageReferences);
                    
                    if (updatedImages.Count > 0)
                    {
                        PushToRemote(repository,
                                     new GitBranchName(applicationSource.TargetRevision),
                                     deploymentConfig.CommitParameters,
                                     updatedFiles,
                                     updatedImages);
                        
                        newImagesWritten.UnionWith(updatedImages);
                        updatedApplications.Add(applicationFromYaml.Metadata.Name);
                        gitReposUpdated.Add(applicationSource.RepoUrl.AbsoluteUri);
                    }
                }
                
                var valuesFilesToUpdate = new HelmValuesFileUpdateTargetParser(applicationFromYaml).GetValuesFilesToUpdate();
                foreach (var valuesFileSource in valuesFilesToUpdate)
                {
                    if (valuesFileSource is InvalidHelmValuesFileImageUpdateTarget invalidSource)
                    {
                        log.Warn($"Invalid annotations setup detected.\nAlias defined: {invalidSource.Alias}. Missing corresponding {ArgoCDConstants.Annotations.OctopusImageReplacementPathsKeyWithSpecifier(invalidSource.Alias)} annotation.");
                        continue;
                    }

                    var helmUpdateResult = HandleHelmValuesTarget(valuesFileSource,
                                                                  helmImageUpdater,
                                                                  deploymentConfig,
                                                                  CancellationToken.None);
                    if (helmUpdateResult.ImagesUpdated.Count > 0)
                    {
                        newImagesWritten.UnionWith(helmUpdateResult.ImagesUpdated);
                        updatedApplications.Add(applicationFromYaml.Metadata.Name);
                        gitReposUpdated.Add(valuesFileSource.RepoUrl.ToString());
                    }
                }
            }
            
            var outputWriter = new ArgoCDImageUpdateOutputWriter(log);
            outputWriter.WriteImageUpdateOutput(gatewayIds,
                                                gitReposUpdated,
                                                argoProperties.Applications.Select(a => a.Name),
                                                updatedApplications.Distinct(),
                                                newImagesWritten.Count
                                                );
        }

        (HashSet<string>, HashSet<string>) UpdateKubernetesYaml(string rootPath,
                                                                string subFolder,
                                                                string defaultRegistry,
                                                                List<ContainerImageReference> imagesToUpdate)
        {
            var absSubFolder = Path.Combine(rootPath, subFolder);
            var yamlFiles = FindYamlFiles(absSubFolder);

            var updatedFiles = new HashSet<string>();
            var updatedImages = new HashSet<string>();
            foreach (var file in yamlFiles)
            {
                var relativePath = Path.GetRelativePath(rootPath, file);
                log.Verbose($"Processing file {relativePath}.");
                var fileContent = fileSystem.ReadFile(file);

                var imageReplacer = new ContainerImageReplacer(fileContent, defaultRegistry);
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
        
        HelmRefUpdatedResult HandleHelmValuesTarget(HelmValuesFileImageUpdateTarget updateTarget, IArgoCDHelmVariablesImageUpdater imageUpdater,  UpdateArgoCDAppDeploymentConfig config, CancellationToken ct)
        {
            try
            {
                var helmUpdateResult = imageUpdater.UpdateImages(updateTarget,
                                                                 config.ImageReferences,
                                                                 new GitCommitMessage("summary", "body"), //TODO(tmm): this is where everything kinda changes
                                                                 config.CommitParameters.RequiresPr,
                                                                       log,
                                                                       ct).GetAwaiter().GetResult();
                return helmUpdateResult;
            }
            catch (Exception ex)
            {
                log.Error($"Failed to update images for Argo CD app {updateTarget.Name} at {updateTarget.RepoUrl}: {ex.Message}");
                throw new CommandException($"Failed to update Helm images for Argo CD app {updateTarget.AppName}.", ex);
            }
        }

        
        void PushToRemote(RepositoryWrapper repository,
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
                repository.PushChanges(commitParameters.RequiresPr,  commitParameters.Summary, commitDescription, branchName, CancellationToken.None).GetAwaiter().GetResult();
            }
            else
            {
                Log.Info("No changes were commited.");
            }
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

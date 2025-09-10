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
        readonly ArgoCommitToGitConfigFactory argoCommitToGitConfigFactory;
        readonly ICommitMessageGenerator commitMessageGenerator;
        readonly ICustomPropertiesLoader customPropertiesLoader;

        public UpdateArgoCDAppImagesInstallConvention(ILog log,
                                                      IGitHubPullRequestCreator pullRequestCreator,
                                                      ICalamariFileSystem fileSystem,
                                                      ArgoCommitToGitConfigFactory argoCommitToGitConfigFactory,
                                                      ICommitMessageGenerator commitMessageGenerator,
                                                      ICustomPropertiesLoader customPropertiesLoader)
        {
            this.log = log;
            this.pullRequestCreator = pullRequestCreator;
            this.fileSystem = fileSystem;
            this.argoCommitToGitConfigFactory = argoCommitToGitConfigFactory;
            this.commitMessageGenerator = commitMessageGenerator;
            this.customPropertiesLoader = customPropertiesLoader;
        }

        public void Install(RunningDeployment deployment)
        {
            var actionConfig = argoCommitToGitConfigFactory.Create(deployment);
            var repositoryFactory = new RepositoryFactory(log, deployment.CurrentDirectory, pullRequestCreator);
            var packageReferences = deployment.Variables.GetContainerPackageNames().Select(p => ContainerImageReference.FromReferenceString(p)).ToList();
            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();

            int repositoryNumber = 1;
            var updatedApplications = new List<string>();
            var newImagesWritten = new HashSet<string>();
            var gitReposUpdated = new HashSet<Uri>();
            var updatedFiles = new HashSet<string>();

            Log.Info($"Updating {argoProperties.Applications.Length} applications.");
            foreach (var application in argoProperties.Applications)
            {
                Log.Info($"Updating '{application.Name}'");
                // update the directory apps, THEN do the reference work - needs to work more like the Server Solution
                var directorySources = application.Sources.Where(s => s.SourceType.Equals("Directory", StringComparison.OrdinalIgnoreCase));
                foreach (var applicationSource in directorySources)
                {
                    var repoPath = repositoryNumber++.ToString(CultureInfo.InvariantCulture);
                    Log.Info($"Writing files to git repository for '{applicationSource.Url}'");
                    var gitConnection = new GitConnection(applicationSource.Username, applicationSource.Password, applicationSource.Url, new GitBranchName(applicationSource.TargetRevision));
                    var repository = repositoryFactory.CloneRepository(repoPath, gitConnection);

                    var updatedImages = HandleBasicSource(repository,
                                                          application.DefaultRegistry,
                                                          applicationSource,
                                                          packageReferences,
                                                          actionConfig);
                    newImagesWritten.UnionWith(updatedImages);
                }

                foreach (var key in application.HelmAnnotations.Keys)
                {
                    var repoPath = repositoryNumber++.ToString(CultureInfo.InvariantCulture);
                    var (files, images) = HandleHelmSource(application,
                                                           repositoryFactory,
                                                           key,
                                                           application.HelmAnnotations[key],
                                                           packageReferences,
                                                           actionConfig,
                                                           repoPath);
                }
            }
        }

        HashSet<string> HandleBasicSource(RepositoryWrapper repository,
                                          string defaultRegistry,
                                          ArgoCDApplicationSourceDto applicationSource,
                                          List<ContainerImageReference> packageReferences,
                                          IGitCommitParameters actionConfig)
        {
            var subFolder = applicationSource.Path ?? String.Empty;
            var (updatedFiles, updatedImages) = UpdateKubernetesYaml(repository.WorkingDirectory, subFolder, defaultRegistry, packageReferences);

            if (updatedFiles.Count > 0)
            {
                PushToRemote(repository,
                             new GitBranchName(applicationSource.TargetRevision),
                             actionConfig,
                             updatedFiles,
                             updatedImages);
            }

            return updatedImages;
        }

        void PushToRemote(RepositoryWrapper repository,
                          GitBranchName branchName,
                          IGitCommitParameters commitParameters,
                          HashSet<string> updatedFiles,
                          HashSet<string> updatedImages)
        {
            Log.Info("Staging files in repository");
            repository.StageFiles(updatedFiles.ToArray());

            var commitMessage = commitMessageGenerator.GenerateForImageUpdates(new GitCommitSummary(commitParameters.CommitSummary), commitParameters.CommitDescription, updatedImages);

            Log.Info("Commiting changes");
            if (repository.CommitChanges(commitMessage))
            {
                Log.Info("Changes were commited, pushing to remote");
                repository.PushChanges(commitParameters.RequiresPr, branchName, CancellationToken.None).GetAwaiter().GetResult();
            }
            else
            {
                Log.Info("No changes were commited.");
            }
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

        //This replicates ArgoCDHelmVariablesImageUpdater.UpdateImages
        HelmRefUpdatedResult HandleHelmSource(ArgoCDApplicationDto app,
                                              RepositoryFactory repositoryFactory,
                                              string refKey,
                                              List<string> imagePathAnnotations,
                                              List<ContainerImageReference> imagesToUpdate,
                                              IGitCommitParameters commitParams,
                                              string repoPath)
        {
            try
            {
                var helmValuesRef = GetHelmImageReference(refKey, app);
                var repository = repositoryFactory.CloneRepository(repoPath, helmValuesRef.GitConnection);
                log.Info($"Processing file at {helmValuesRef.Path}.");
                var valuesFilePath = Path.Combine(repoPath, helmValuesRef.Path);
                var fileContent = fileSystem.ReadFile(valuesFilePath);

                var helmImageReplacer = new HelmContainerImageReplacer(fileContent, app.DefaultRegistry, imagePathAnnotations);
                var imageUpdateResult = helmImageReplacer.UpdateImages(imagesToUpdate);

                if (imageUpdateResult.UpdatedImageReferences.Count > 0)
                {
                    try
                    {
                        fileSystem.OverwriteFile(fileContent, imageUpdateResult.UpdatedContents);
                        PushToRemote(repository,
                                     helmValuesRef.GitConnection.BranchName,
                                     commitParams,
                                     new HashSet<string>() { helmValuesRef.Path },
                                     imageUpdateResult.UpdatedImageReferences);

                        return new HelmRefUpdatedResult(helmValuesRef.GitConnection.Url, imageUpdateResult.UpdatedImageReferences);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to commit changes to the Git Repository: {ex.Message}");
                        throw;
                    }
                }

                return new HelmRefUpdatedResult(helmValuesRef.GitConnection.Url, new HashSet<string>()); // Nothing was changed
            }
            catch (Exception ex)
            {
                throw new CommandException($"Failed to update Helm images for Argo CD app {app.Name}.", ex);
            }
        }

        static HelmValueFileReference GetHelmImageReference(string refKey, ArgoCDApplicationDto app)
        {
            var valueFileReference = app.Sources.Where(s => s.SourceType.Equals("Helm"))
                                        .Select(h => h.Path)
                                        .Distinct()
                                        .Where(vf => vf.StartsWith('$'))
                                        .Select(vf => new
                                        {
                                            RefName = vf.Split('/')[0].TrimStart('$'),
                                            Path = vf.Substring(vf.IndexOf('/') + 1),
                                            FullReference = vf
                                        })
                                        .Join(
                                              app.Sources.Where(s => s.SourceType.Equals("Ref")).Where(r => r.Path == refKey), //TODO(tmm): part of the hack
                                              valueFile => valueFile.RefName,
                                              refSource => refSource.Path, //TODO(tmm): This is a hack to handle the fact that Ref-sources don't _really_ fit into our DTO
                                              (valueFile, refSource) => new HelmValueFileReference(
                                                                                                   valueFile.Path,
                                                                                                   valueFile.FullReference,
                                                                                                   new GitConnection(
                                                                                                                     refSource.Username,
                                                                                                                     refSource.Password,
                                                                                                                     refSource.Url,
                                                                                                                     new GitBranchName(refSource.TargetRevision)
                                                                                                                    ))
                                             )
                                        .FirstOrDefault();

            if (valueFileReference == null)
            {
                throw new InvalidOperationException(refKey);
            }

            return valueFileReference;
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
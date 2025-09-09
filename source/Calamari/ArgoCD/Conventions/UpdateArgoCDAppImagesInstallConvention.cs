#if NET
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;
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

        public UpdateArgoCDAppImagesInstallConvention(ILog log, IGitHubPullRequestCreator pullRequestCreator, ICalamariFileSystem fileSystem, ArgoCommitToGitConfigFactory argoCommitToGitConfigFactory,
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
            foreach (var application in argoProperties.Applications)
            {
                foreach (var applicationSource in application.Sources)
                {
                    Log.Info($"Writing files to git repository for '{applicationSource.Url}'");
                    var gitConnection = new GitConnection(applicationSource.Username, applicationSource.Password, applicationSource.Url, new GitBranchName(applicationSource.TargetRevision));
                    var repository = repositoryFactory.CloneRepository(repositoryNumber.ToString(CultureInfo.InvariantCulture), gitConnection);
                    
                    var subFolder = applicationSource.Path ?? String.Empty;

                    var (updatedFiles, updatedImages) = HandleDirectorySource(repository.WorkingDirectory, subFolder, application.DefaultRegistry, packageReferences);

                    if (updatedFiles.Count > 0)
                    {
                        Log.Info("Staging files in repository");
                        repository.StageFiles(updatedFiles.ToArray());

                        var commitMessage = commitMessageGenerator.GenerateForImageUpdates(new GitCommitSummary(actionConfig.CommitSummary), actionConfig.CommitDescription, updatedImages);

                        Log.Info("Commiting changes");
                        if (repository.CommitChanges(commitMessage))
                        {
                            Log.Info("Changes were commited, pushing to remote");
                            repository.PushChanges(actionConfig.RequiresPr, new GitBranchName(applicationSource.TargetRevision), CancellationToken.None).GetAwaiter().GetResult();
                        }
                        else
                        {
                            Log.Info("No changes were commited.");
                        }
                    }
                }
            }
        }

        (HashSet<string>, HashSet<string>) HandleDirectorySource(string rootPath,
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
        
        HelmRefUpdatedResult HandleHelmSource(ArgoCDApplicationToUpdate app, string refKey, List<string> imagePathAnnotations, IArgoCDHelmVariablesImageUpdater imageUpdater, ArgoCDUpdateActionVariables stepVariables,
                                                                 ILog log, CancellationToken ct)
        {
            try
            {
                var helmUpdateResult = imageUpdater.UpdateImages(app,
                                                                       refKey,
                                                                       imagePathAnnotations,
                                                                       stepVariables.ImageReferences,
                                                                       stepVariables.CommitMessageSummary,
                                                                       stepVariables.CommitMessageDescription,
                                                                       stepVariables.CreatePullRequest,
                                                                       log,
                                                                       ct).GetAwaiter().GetResult();

                return helmUpdateResult;
            }
            catch (Exception ex)
            {
                throw new ActivityFailedException($"Failed to update Helm images for Argo CD app {app.Application.Name}.", ex);
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

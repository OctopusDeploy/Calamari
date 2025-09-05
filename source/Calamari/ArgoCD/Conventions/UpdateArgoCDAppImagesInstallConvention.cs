#if NET
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppImagesInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly IGitHubPullRequestCreator pullRequestCreator;
        readonly ArgoCommitToGitConfigFactory argoCommitToGitConfigFactory;
        readonly CommitMessageGenerator commitMessageGenerator;

        public UpdateArgoCDAppImagesInstallConvention(ILog log, IGitHubPullRequestCreator pullRequestCreator, ICalamariFileSystem fileSystem, ArgoCommitToGitConfigFactory argoCommitToGitConfigFactory,
                                                      CommitMessageGenerator commitMessageGenerator)
        {
            this.log = log;
            this.pullRequestCreator = pullRequestCreator;
            this.fileSystem = fileSystem;
            this.argoCommitToGitConfigFactory = argoCommitToGitConfigFactory;
            this.commitMessageGenerator = commitMessageGenerator;
        }

        public void Install(RunningDeployment deployment)
        {
            var actionConfig = argoCommitToGitConfigFactory.Create(deployment);
            var repositoryFactory = new RepositoryFactory(log, deployment.CurrentDirectory, pullRequestCreator);
            var packageReferences = deployment.Variables.GetContainerPackageNames().Select(p => ContainerImageReference.FromReferenceString(p)).ToList();
            
            var repositoryIndex = 0;
            foreach (var argoSource in actionConfig.ArgoSourcesToUpdate)
            {
                var repoName = repositoryIndex.ToString();
                Log.Info($"Writing files to git repository for '{argoSource.Url}'");
                var repository = repositoryFactory.CloneRepository(repoName, argoSource);

                string defaultRegistry = ""; // deployment.Variables.Get(SpecialVariables.Git.DefaultRegistry(repositoryIndex));

                var pathToUpdate = Path.Combine(repository.WorkingDirectory, argoSource.SubFolder);

                var (updatedFiles, updatedImages) = UpdateFiles(pathToUpdate, defaultRegistry, packageReferences);

                if (updatedFiles.Count > 0)
                {
                    Log.Info("Staging files in repository");
                    repository.StageFiles(updatedFiles.ToArray());

                    var commitMessage = commitMessageGenerator.GenerateForImageUpdates(new GitCommitSummary(actionConfig.CommitSummary), actionConfig.CommitDescription, updatedImages);
                    
                    Log.Info("Commiting changes");
                    if (repository.CommitChanges(commitMessage))
                    {
                        Log.Info("Changes were commited, pushing to remote");
                        repository.PushChanges(actionConfig.RequiresPr, argoSource.BranchName, CancellationToken.None).GetAwaiter().GetResult();    
                    }
                    else
                    {
                        Log.Info("No changes were commited.");
                    }
                }
            }
        }

        (HashSet<string>, HashSet<string>) UpdateFiles(string rootPath,
                                                       string defaultRegistry,
                                                       List<ContainerImageReference> imagesToUpdate)
        {
            var yamlFiles = FindYamlFiles(rootPath);

            var updatedFiles = new HashSet<string>();
            var updatedImages = new HashSet<string>();
            foreach (var file in yamlFiles)
            {
                log.Verbose($"Processing file {file}.");
                var fileContent = fileSystem.ReadFile(file);

                var imageReplacer = new ContainerImageReplacer(fileContent, defaultRegistry);
                var imageReplacementResult = imageReplacer.UpdateImages(imagesToUpdate);

                if (imageReplacementResult.UpdatedImageReferences.Count > 0)
                {
                    fileSystem.OverwriteFile(file, imageReplacementResult.UpdatedContents);
                    updatedImages.UnionWith(imageReplacementResult.UpdatedImageReferences);
                    updatedFiles.Add(file);
                    log.Verbose($"Updating file {file} with new image references.");
                    foreach (var change in imageReplacementResult.UpdatedImageReferences)
                    {
                        log.Verbose($"Updated image reference: {change}");
                    }
                }
                else
                {
                    log.Verbose($"No changes made to file {file} as no image references were updated.");
                }
            }

            return (updatedFiles, updatedImages);
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
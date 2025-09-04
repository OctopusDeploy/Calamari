#if NET
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

        public UpdateArgoCDAppImagesInstallConvention(ILog log, IGitHubPullRequestCreator pullRequestCreator, ICalamariFileSystem fileSystem, ArgoCommitToGitConfigFactory argoCommitToGitConfigFactory)
        {
            this.log = log;
            this.pullRequestCreator = pullRequestCreator;
            this.fileSystem = fileSystem;
            this.argoCommitToGitConfigFactory = argoCommitToGitConfigFactory;
        }

        public void Install(RunningDeployment deployment)
        {
            var actionConfig = argoCommitToGitConfigFactory.Create(deployment);
            var repositoryFactory = new RepositoryFactory(log, deployment.CurrentDirectory, pullRequestCreator);
            var packageReferences = deployment.Variables.GetContainerPackageNames().Select(p => ContainerImageReference.FromReferenceString(p)).ToList();

            var repositoryIndexes = deployment.Variables.GetIndexes(SpecialVariables.Git.Index);
            var repositoryIndex = 0;
            foreach (var argoSource in actionConfig.ArgoSourcesToUpdate)
            {
                var repoName = repositoryIndex.ToString();
                Log.Info($"Writing files to git repository for '{argoSource.Url}'");
                var repository = repositoryFactory.CloneRepository(repoName, argoSource);
                
                var defaultRegistry = deployment.Variables.Get(SpecialVariables.Git.DefaultRegistry(repositoryIndex));

                var pathToUpdate = Path.Combine(repository.WorkingDirectory, argoSource.SubFolder);
                var updatedFiles = UpdateFiles(gitConnection, pathToUpdate, defaultRegistry, packageReferences);

                if (updatedFiles.Count > 0)
                {
                    Log.Info("Staging files in repository");
                    repository.StageFiles(updatedFiles.ToArray());

                    Log.Info("Commiting changes");
                    var commitMessage = GenerateCommitMessage(new GitCommitSummary(commitMessageSummary), commitMessageDescription);
                    if (repository.CommitChanges(commitMessage))
                    {
                        Log.Info("Changes were commited, pushing to remote");
                        repository.PushChanges(requiresPullRequest, gitConnection.BranchName, CancellationToken.None).GetAwaiter().GetResult();
                    }
                    else
                    {
                        Log.Info("No changes were commited.");
                    }
                }
            }
        }

        bool RequiresPullRequest(RunningDeployment deployment)
        {
            return OctopusFeatureToggles.ArgoCDCreatePullRequestFeatureToggle.IsEnabled(deployment.Variables) && deployment.Variables.Get(SpecialVariables.Git.CommitMethod) == SpecialVariables.Git.GitCommitMethods.PullRequest;
        }

        string GenerateCommitMessage(GitCommitSummary summary, string commitMessageDescription)
        {
            return "this is a commit message";
        }

        public HashSet<string> UpdateFiles(IGitConnection toUpdate,
                                           string rootPath,
                                           string defaultRegistry,
                                           List<ContainerImageReference> imagesToUpdate)
        {
            var imagesReplaced = new HashSet<string>();
            log.Info($"App to update: {toUpdate.Url}");

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
            
            if (updatedFiles.Count > 0)
            {
                try
                {
                    var imageUpdateChanges = new ImageUpdateChanges(updatedFiles, updatedImages);
            
                    var changesApplied = await repository.TryCommitChanges(imageUpdateChanges,
                                                                           commitSummary,
                                                                           userCommitDescription,
                                                                           log,
                                                                           cancellationToken);
                    if (changesApplied)
                    {
                        imagesReplaced.UnionWith(updatedImages);
                    }
                    else
                    {
                        // NOTE: We need to look at how we can provide better information to the user as to WHY the commit was not made.
                        // This could be because:
                        // - Changes were redundant (unlikely)
                        // - The parent commit was superseded by another commit,
                        // - Failure for some other reason
                        log.Warn($"Changes were not committed to {toUpdate.Url}.");
                    }
                }
                catch (Exception e)
                {
                    log.Error($"Failed to commit changes to the Git Repository: {e.Message}");
                    throw;
                }
            }

            return imagesReplaced;
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
#if NET
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.Common.Plumbing.Logging;
using Octopus.Core.Features.Kubernetes.ArgoCD;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages
{
    public interface IArgoCDAppImageUpdater
    {
        Task<HashSet<string>> UpdateImages(ArgoCDImageUpdateTarget toUpdate,
                                           List<ContainerImageReference> imagesToUpdate,
                                           GitCommitSummary commitSummary,
                                           string? userCommitDescription,
                                           bool createPullRequest,
                                           ILog log,
                                           CancellationToken cancellationToken);
    }

    public class ArgoCDAppImageUpdater : IArgoCDAppImageUpdater
    {
        public ArgoCDAppImageUpdater(IGitOpsRepositoryFactory gitOpsRepositoryFactory)
        {
            this.gitOpsRepositoryFactory = gitOpsRepositoryFactory;
        }

        readonly IGitOpsRepositoryFactory gitOpsRepositoryFactory;

        public async Task<HashSet<string>> UpdateImages(ArgoCDImageUpdateTarget toUpdate,
                                                        List<ContainerImageReference> imagesToUpdate,
                                                        GitCommitSummary commitSummary,
                                                        string? userCommitDescription,
                                                        bool createPullRequest,
                                                        ILog log,
                                                        CancellationToken cancellationToken)
        {
            var imagesReplaced = new HashSet<string>();
            log.Info($"App to update: {toUpdate.RepoUrl}");

            var repository = await gitOpsRepositoryFactory.Create(toUpdate, log, createPullRequest, cancellationToken);

            var filesToUpdate = await repository.ReadYamlFiles(log, cancellationToken);

            var updatedFiles = new Dictionary<string, string>();
            var updatedImages = new HashSet<string>();
            foreach (var file in filesToUpdate)
            {
                log.Verbose($"Processing file {file.Key} in Repository {toUpdate.RepoUrl}.");

                var imageReplacer = new ContainerImageReplacer(file.Value, toUpdate.DefaultClusterRegistry);

                var imageReplacementResult = imageReplacer.UpdateImages(imagesToUpdate);

                if (imageReplacementResult.UpdatedImageReferences.Count > 0)
                {
                    updatedImages.UnionWith(imageReplacementResult.UpdatedImageReferences);
                    updatedFiles.Add(file.Key, imageReplacementResult.UpdatedContents);
                    log.Verbose($"Updating file {file.Key} with new image references.");
                    foreach (var change in imageReplacementResult.UpdatedImageReferences)
                    {
                        log.Verbose($"Updated image reference: {change}");
                    }
                }
                else
                {
                    log.Verbose($"No changes made to file {file.Key} as no image references were updated.");
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
                        log.Warn($"Changes were not committed to {toUpdate.RepoUrl}.");
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

    }
}

#endif

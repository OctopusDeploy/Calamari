using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Commands.Executors.Models;
using Calamari.Common.Plumbing.Logging;
using Octopus.Core.Features.Kubernetes.ArgoCd.Models;

namespace Calamari.ArgoCD.Commands.Executors;

public interface IArgoCDAppImageUpdater
{
    Task<HashSet<string>> UpdateImages(ArgoCDImageUpdateTarget toUpdate, List<ContainerImageReference> imagesToUpdate, string? userCommitDescription, CancellationToken cancellationToken);
}

public class ArgoCDAppImageUpdater() : IArgoCDAppImageUpdater
{
    readonly ILog log;
    
    public async Task<HashSet<string>> UpdateImages(ArgoCDImageUpdateTarget toUpdate, List<ContainerImageReference> imagesToUpdate, string? userCommitDescription, CancellationToken cancellationToken)
    {
        var imagesReplaced = new HashSet<string>();
        log.Info($"App to update: {toUpdate.RepoUrl}");
        

        var filesToUpdate = await repository.ReadYamlFiles(taskLog, cancellationToken);
        
        var updatedFiles = new Dictionary<string, string>();
        var updatedImages = new HashSet<string>();
        foreach (var file in filesToUpdate)
        {
            taskLog.Verbose($"Processing file {file.Key} in Repository {toUpdate.RepoUrl}.");
            
            var imageReplacer = new ContainerImageReplacer(file.Value, toUpdate.DefaultClusterRegistry);

            var imageReplacementResult = imageReplacer.UpdateImages(imagesToUpdate);

            if (imageReplacementResult.UpdatedImageReferences.Count > 0)
            {
                updatedImages.UnionWith(imageReplacementResult.UpdatedImageReferences);
                updatedFiles.Add(file.Key, imageReplacementResult.UpdatedContents);
                taskLog.Verbose($"Updating file {file.Key} with new image references.");
                foreach (var change in imageReplacementResult.UpdatedImageReferences)
                {
                    taskLog.Verbose($"Updated image reference: {change}");
                }
            }
            else
            {
                taskLog.Verbose($"No changes made to file {file.Key} as no image references were updated.");
            }
        }

        if (updatedFiles.Count > 0)
        {
            try
            {
                var commit = await repository.CommitChanges(new ImageUpdateChanges(updatedFiles, updatedImages), commitSummary, userCommitDescription,  cancellationToken);
                if (commit != null)
                {
                    imagesReplaced.UnionWith(updatedImages);
                    taskLog.Info($"Changes committed to Git repository {toUpdate.RepoUrl} with commit ID: {commit}");
                }
                else
                {
                    // NOTE: We need to look at how we can provide better information to the user as to WHY the commit was not made.
                    // This could be because:
                    // - Changes were redundant (unlikely)
                    // - The parent commit was superseded by another commit,
                    // - Failure for some other reason
                    taskLog.Warn($"Changes were not committed to {toUpdate.RepoUrl}.");
                }
            }
            catch (Exception e)
            {
                taskLog.Error($"Failed to commit changes to the Git Repository: {e.Message}");
                throw;
            }
        }
        return imagesReplaced;
    }

}

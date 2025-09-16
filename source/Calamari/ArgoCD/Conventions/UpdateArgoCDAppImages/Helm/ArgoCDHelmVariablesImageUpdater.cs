#if NET
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.ArgoCD.Git;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Helm
{
    public record HelmRefUpdatedResult(Uri RepoUrl, HashSet<string> ImagesUpdated);

    public interface IArgoCDHelmVariablesImageUpdater
    {
        Task<HelmRefUpdatedResult> UpdateImages(HelmValuesFileImageUpdateTarget target,
                                                List<ContainerImageReference> imagesToUpdate,
                                                GitCommitMessage commitMessage,
                                                bool createPullRequest,
                                                ILog log,
                                                CancellationToken ct);
    }

    public class ArgoCDHelmVariablesImageUpdater(IGitOpsRepositoryFactory gitOpsRepositoryFactory) : IArgoCDHelmVariablesImageUpdater
    {
        public async Task<HelmRefUpdatedResult> UpdateImages(HelmValuesFileImageUpdateTarget target,
                                                             List<ContainerImageReference> imagesToUpdate,
                                                             GitCommitMessage commitMessage,
                                                             bool createPullRequest,
                                                             ILog log,
                                                             CancellationToken ct)
        {
            var repository = await gitOpsRepositoryFactory.Create(target, log, createPullRequest, ct);

            var fileContent = await repository.ReadFileContents(target.FileName, ct);
            log.Info($"Processing file at {target.FileName}.");

            var helmImageReplacer = new HelmContainerImageReplacer(fileContent, target.DefaultClusterRegistry, target.ImagePathDefinitions);
            var imageUpdateResult = helmImageReplacer.UpdateImages(imagesToUpdate);
            var updatedFiles = new Dictionary<string, string>();
            if (imageUpdateResult.UpdatedImageReferences.Count > 0)
            {
                try
                {
                    updatedFiles.Add(target.FileName, imageUpdateResult.UpdatedContents);
                    var imageUpdateChanges = new ImageUpdateChanges(updatedFiles, imageUpdateResult.UpdatedImageReferences);
                    await repository.TryCommitChanges(imageUpdateChanges, commitMessage, log, ct);
                    return new HelmRefUpdatedResult(target.RepoUrl, imageUpdateResult.UpdatedImageReferences);
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to commit changes to the Git Repository: {ex.Message}");
                    throw;
                }
            }

            return new HelmRefUpdatedResult(target.RepoUrl, []);
        }
    }
}
#endif
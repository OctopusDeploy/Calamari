#if NET
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Helm;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.Common.Plumbing.Logging;
using Octopus.Core.Features.Kubernetes.ArgoCD.Models;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages
{

    public record HelmRefUpdatedResult(Uri RepoUrl, HashSet<string> ImagesUpdated);

    public interface IArgoCDHelmVariablesImageUpdater
    {
        Task<HelmRefUpdatedResult> UpdateImages(ArgoCDApplicationToUpdate app,
                                                string refKey,
                                                List<string> imagePathAnnotations,
                                                List<ContainerImageReference> imagesToUpdate,
                                                GitCommitSummary commitSummary,
                                                string? userCommitDescription,
                                                bool createPullRequest,
                                                ILog log,
                                                CancellationToken ct);
    }

    public class ArgoCDHelmVariablesImageUpdater(IGitOpsRepositoryFactory gitOpsRepositoryFactory) : IArgoCDHelmVariablesImageUpdater
    {
        static HelmValueFileReference GetHelmImageReference(string refKey, ArgoCDApplicationToUpdate app)
        {
            var valueFileReference = app.Application.ApplicationSources.OfType<ArgoCDHelmApplicationSource>()
                                        .SelectMany(h => h.ValueFiles)
                                        .Distinct()
                                        .Where(vf => vf.StartsWith('$'))
                                        .Select(vf => new
                                        {
                                            RefName = vf.Split('/')[0].TrimStart('$'),
                                            Path = vf.Substring(vf.IndexOf('/') + 1),
                                            FullReference = vf
                                        })
                                        .Join(
                                              app.Application.ApplicationSources.OfType<ArgoCDRefApplicationSource>().Where(r => r.Ref == refKey),
                                              valueFile => valueFile.RefName,
                                              refSource => refSource.Ref,
                                              (valueFile, refSource) => new HelmValueFileReference(
                                                                                                   valueFile.Path,
                                                                                                   valueFile.FullReference,
                                                                                                   refSource
                                                                                                  )
                                             )
                                        .FirstOrDefault();

            if (valueFileReference == null)
            {
                throw new InvalidSourceRefException(refKey);
            }

            return valueFileReference;
        }

        public async Task<HelmRefUpdatedResult> UpdateImages(ArgoCDApplicationToUpdate app,
                                                             string refKey,
                                                             List<string> imagePathAnnotations,
                                                             List<ContainerImageReference> imagesToUpdate,
                                                             GitCommitSummary commitSummary,
                                                             string? userCommitDescription,
                                                             bool createPullRequest,
                                                             ILog log,
                                                             CancellationToken ct)
        {
            // Get the specified Ref soruce from the application.
            var helmValuesRef = GetHelmImageReference(refKey, app);

            var repository = await gitOpsRepositoryFactory.Create(new ArgoCDImageUpdateTarget(helmValuesRef.RefSource.Ref,
                                                                                              app.DefaultClusterRegistry,
                                                                                              "./",
                                                                                              helmValuesRef.RefSource.RepositoryUrl,
                                                                                              helmValuesRef.RefSource.TargetRevision),
                                                                  log,
                                                                  createPullRequest,
                                                                  ct);

            var fileContent = await repository.ReadFileContents(helmValuesRef.Path, ct);

            log.Info($"Processing file at {helmValuesRef.Path}.");
            var helmImageReplacer = new HelmContainerImageReplacer(fileContent, app.DefaultClusterRegistry, imagePathAnnotations);
            var imageUpdateResult = helmImageReplacer.UpdateImages(imagesToUpdate);

            var updatedFiles = new Dictionary<string, string>();
            if (imageUpdateResult.UpdatedImageReferences.Count > 0)
            {
                try
                {
                    updatedFiles.Add(helmValuesRef.Path, imageUpdateResult.UpdatedContents);
                    var imageUpdateChanges = new ImageUpdateChanges(updatedFiles, imageUpdateResult.UpdatedImageReferences);
                    await repository.TryCommitChanges(imageUpdateChanges,
                                                      commitSummary,
                                                      userCommitDescription,
                                                      log,
                                                      ct);
                    return new HelmRefUpdatedResult(helmValuesRef.RefSource.RepositoryUrl, imageUpdateResult.UpdatedImageReferences);
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to commit changes to the Git Repository: {ex.Message}");
                    throw;
                }
            }

            return new HelmRefUpdatedResult(helmValuesRef.RefSource.RepositoryUrl, new HashSet<string>()); // Nothing was changed
        }
    }
}
#endif
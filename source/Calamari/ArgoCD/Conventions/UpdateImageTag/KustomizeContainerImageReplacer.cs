using System.Collections.Generic;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class KustomizeContainerImageReplacer : IContainerImageReplacer
{
    readonly string input;
    readonly string defaultRegistry;
    readonly bool updateKustomizePatches;
    readonly ILog log;
    readonly KustomizeDiscovery discovery;

    public KustomizeContainerImageReplacer(string input, string defaultRegistry, bool updateKustomizePatches, ILog log)
    {
        this.input = input;
        this.defaultRegistry = defaultRegistry;
        this.updateKustomizePatches = updateKustomizePatches;
        this.log = log;
        discovery = new KustomizeDiscovery(log);
    }

    public ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        // Create a copy of input to avoid modifying the original data
        var inputCopy = input;

        if (KustomizationValidator.IsKustomizationResource(inputCopy))
        {
            return UpdateKustomizeResource(imagesToUpdate, inputCopy);
        }


        if (updateKustomizePatches)
        {
            return UpdateKustomizePatch(imagesToUpdate, inputCopy);
        }

        return new ImageReplacementResult(inputCopy, new HashSet<string>(), new HashSet<string>());
    }

    ImageReplacementResult UpdateKustomizeResource(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, string inputContent)
    {
        var updatedContent = inputContent;
        var allUpdatedImages = new HashSet<string>();

        var kustomizeReplacer = new KustomizeImageReplacer(updatedContent, defaultRegistry, log);
        var result = kustomizeReplacer.UpdateImages(imagesToUpdate);

        if (result.UpdatedImageReferences.Count > 0)
        {
            updatedContent = result.UpdatedContents;
            allUpdatedImages.UnionWith(result.UpdatedImageReferences);
        }

        if (updateKustomizePatches)
        {
            if (KustomizePatchDiscovery.HasPatchesNode(inputContent, log))
            {
                var inlinePatchReplacer = new InlineJsonPatchReplacer(updatedContent, defaultRegistry, log);
                var patchResult = inlinePatchReplacer.UpdateImages(imagesToUpdate);

                if (patchResult.UpdatedImageReferences.Count > 0)
                {
                    updatedContent = patchResult.UpdatedContents;
                    allUpdatedImages.UnionWith(patchResult.UpdatedImageReferences);
                }
            }

            if (KustomizePatchDiscovery.HasStrategicMergePatchNode(inputContent, log))
            {
                var replacer = new InlineStrategicMergeImageReplacer(updatedContent, defaultRegistry, log);
                var strategicMergeResult = replacer.UpdateImages(imagesToUpdate);

                if (strategicMergeResult.UpdatedImageReferences.Count > 0)
                {
                    updatedContent = strategicMergeResult.UpdatedContents;
                    allUpdatedImages.UnionWith(strategicMergeResult.UpdatedImageReferences);
                }
            }

            if (KustomizePatchDiscovery.HasJson6902PatchesNode(inputContent, log))
            {
                var replacer = new InlineJson6902ImageReplacer(updatedContent, defaultRegistry, log);
                var json6902Result = replacer.UpdateImages(imagesToUpdate);

                if (json6902Result.UpdatedImageReferences.Count > 0)
                {
                    updatedContent = json6902Result.UpdatedContents;
                    allUpdatedImages.UnionWith(json6902Result.UpdatedImageReferences);
                }
            }
        }

        return new ImageReplacementResult(updatedContent, allUpdatedImages, new HashSet<string>());
    }

    ImageReplacementResult UpdateKustomizePatch(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, string inputContent)
    {
        var patchType = discovery.DeterminePatchType(inputContent);

        IContainerImageReplacer replacer = patchType switch
        {
            PatchType.StrategicMerge => new ContainerImageReplacer(inputContent, defaultRegistry),
            PatchType.Json6902 => new JsonPatchImageReplacer(inputContent, defaultRegistry, log),
            _ => null
        };

        if (replacer != null)
        {
            return replacer.UpdateImages(imagesToUpdate);
        }

        log.Verbose($"Unable to determine patch type for content, no image updates will be performed");
        return new ImageReplacementResult(inputContent, new HashSet<string>(), new HashSet<string>());
    }

}
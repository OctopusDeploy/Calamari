using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using NuGet.Packaging;

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

        var kustomizeReplacer = new KustomizeImageReplacer(inputContent, defaultRegistry, log);
        var result = kustomizeReplacer.UpdateImages(imagesToUpdate);

        if (updateKustomizePatches)
        {
            if (KustomizePatchDiscovery.HasPatchesNode(inputContent, log))
            {
                var inlinePatchReplacer = new InlineJsonPatchReplacer(result.UpdatedContents, defaultRegistry, log);
                var patchResult = inlinePatchReplacer.UpdateImages(imagesToUpdate);
                result = MergeResults(result, patchResult);
            }

            if (KustomizePatchDiscovery.HasStrategicMergePatchNode(inputContent, log))
            {
                var replacer = new InlineStrategicMergeImageReplacer(result.UpdatedContents, defaultRegistry, log);
                var strategicMergeResult = replacer.UpdateImages(imagesToUpdate);
                result = MergeResults(result, strategicMergeResult);
            }

            if (KustomizePatchDiscovery.HasJson6902PatchesNode(inputContent, log))
            {
                var replacer = new InlineJson6902ImageReplacer(result.UpdatedContents, defaultRegistry, log);
                var json6902Result = replacer.UpdateImages(imagesToUpdate);
                result = MergeResults(result, json6902Result);
            }
        }

        return result;
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
    
    ImageReplacementResult MergeResults(ImageReplacementResult existingResult, ImageReplacementResult toInclude) 
    {
        string finalContent = existingResult.UpdatedContents; 
        if (toInclude.UpdatedImageReferences.Count > 0)
        {
            finalContent = toInclude.UpdatedContents;
        }

        var updatedImageReferences = new HashSet<string>(existingResult.UpdatedImageReferences);
        updatedImageReferences.UnionWith(toInclude.UpdatedImageReferences);
        
        var alreadyUpToDateImages = new HashSet<string>(existingResult.AlreadyUpToDateImages);
        alreadyUpToDateImages.UnionWith(toInclude.AlreadyUpToDateImages);
        
        var unrecognisedKinds = new HashSet<string>(existingResult.UnrecognisedKinds);
        unrecognisedKinds.UnionWith(toInclude.UnrecognisedKinds);
        
        
        return new ImageReplacementResult(finalContent, updatedImageReferences, alreadyUpToDateImages, unrecognisedKinds);
    }

}
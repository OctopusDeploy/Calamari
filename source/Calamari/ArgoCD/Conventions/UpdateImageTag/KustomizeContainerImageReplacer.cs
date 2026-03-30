using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class KustomizeContainerImageReplacer : IContainerImageReplacer
{
    readonly string input;
    readonly string defaultRegistry;
    readonly ILog log;
    readonly KustomizeDiscovery discovery;

    public KustomizeContainerImageReplacer(string input, string defaultRegistry, ILog log)
    {
        this.input = input;
        this.defaultRegistry = defaultRegistry;
        this.log = log;
        discovery = new KustomizeDiscovery(log);
    }

    public ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        if (KustomizationValidator.IsKustomizationResource(input))
        {
            return UpdateKustomizeResource(imagesToUpdate);
        }

        return UpdateKustomizePatch(imagesToUpdate);
    }

    ImageReplacementResult UpdateKustomizeResource(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var updatedContent = input;
        var allUpdatedImages = new HashSet<string>();

        var kustomizeReplacer = new KustomizeImageReplacer(updatedContent, defaultRegistry, log);
        var result = kustomizeReplacer.UpdateImages(imagesToUpdate);

        if (result.UpdatedImageReferences.Count > 0)
        {
            updatedContent = result.UpdatedContents;
            allUpdatedImages.UnionWith(result.UpdatedImageReferences);
        }


        if (HasInlinePatches(input))
        {
            var inlinePatchReplacer = new InlineJsonPatchImageReplacer(updatedContent, defaultRegistry, log);
            var patchResult = inlinePatchReplacer.UpdateImages(imagesToUpdate);

            if (patchResult.UpdatedImageReferences.Count > 0)
            {
                updatedContent = patchResult.UpdatedContents;
                allUpdatedImages.UnionWith(patchResult.UpdatedImageReferences);
            }
        }

        if (HasInlineStrategicMergePatches(input))
        {
            var strategicMergeResult = ProcessInlineStrategicMergePatches(updatedContent, imagesToUpdate);

            if (strategicMergeResult.UpdatedImageReferences.Count > 0)
            {
                updatedContent = strategicMergeResult.UpdatedContents;
                allUpdatedImages.UnionWith(strategicMergeResult.UpdatedImageReferences);
            }
        }

        if (HasInlineJson6902Patches(input))
        {
            var json6902Result = ProcessInlineJson6902Patches(updatedContent, imagesToUpdate);

            if (json6902Result.UpdatedImageReferences.Count > 0)
            {
                updatedContent = json6902Result.UpdatedContents;
                allUpdatedImages.UnionWith(json6902Result.UpdatedImageReferences);
            }
        }

        return new ImageReplacementResult(updatedContent, allUpdatedImages, new HashSet<string>());
    }

    ImageReplacementResult UpdateKustomizePatch(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var patchType = discovery.DeterminePatchType(input);

        IContainerImageReplacer replacer = patchType switch
        {
            PatchType.StrategicMerge => new StrategicMergePatchImageReplacer(input, defaultRegistry, log),
            PatchType.Json6902 => new JsonPatchImageReplacer(input, defaultRegistry, log),
            _ => null
        };

        if (replacer != null)
        {
            return replacer.UpdateImages(imagesToUpdate);
        }

        log.Verbose($"Unable to determine patch type for content, no image updates will be performed");
        return new ImageReplacementResult(input, new HashSet<string>(), new HashSet<string>());
    }
    
    internal bool HasInlinePatches(string content)
    {
        var mappingNode = YamlStreamLoader.TryLoadFirstMappingNode(content, log, "inline patches");
        return mappingNode?.Children.ContainsKey(new YamlScalarNode("patches")) ?? false;
    }

    internal bool HasInlineStrategicMergePatches(string content)
    {
        var mappingNode = YamlStreamLoader.TryLoadFirstMappingNode(content, log, "inline strategic merge patches");
        if (mappingNode == null)
            return false;

        if (mappingNode.Children.TryGetValue(new YamlScalarNode("patchesStrategicMerge"), out var patchesNode))
        {
            if (patchesNode is YamlSequenceNode sequence)
            {
                // Look for inline YAML patches (multi-line strings starting with |)
                return sequence.Children.Any(node => node is YamlScalarNode scalar &&
                                                     scalar.Style == ScalarStyle.Literal);
            }
        }

        return false;
    }

    internal bool HasInlineJson6902Patches(string content)
    {
        var mappingNode = YamlStreamLoader.TryLoadFirstMappingNode(content, log, "inline JSON 6902 patches");
        if (mappingNode == null)
            return false;

        if (mappingNode.Children.TryGetValue(new YamlScalarNode("patchesJson6902"), out var patchesNode))
        {
            if (patchesNode is YamlSequenceNode sequence)
            {
                return sequence.Children.OfType<YamlMappingNode>().Any(patchEntry =>
                    patchEntry.Children.TryGetValue(new YamlScalarNode("patch"), out var patchContent) &&
                    patchContent is YamlScalarNode patchScalar &&
                    patchScalar.Style == ScalarStyle.Literal);
            }
        }

        return false;
    }

    ImageReplacementResult ProcessInlineStrategicMergePatches(string content, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var yamlStream = YamlStreamLoader.TryLoad(content, log, "inline strategic merge patches");
        if (yamlStream?.Documents.Count != 1 || !(yamlStream.Documents[0].RootNode is YamlMappingNode rootNode))
        {
            return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());
        }

        if (!rootNode.Children.TryGetValue(new YamlScalarNode("patchesStrategicMerge"), out var patchesNode) || !(patchesNode is YamlSequenceNode patchSequence))
        {
            return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());
        }

        var allUpdatedImages = new HashSet<string>();
        foreach (var patchNode in patchSequence.Children)
        {
            if (patchNode is YamlScalarNode patchScalar && patchScalar.Style == ScalarStyle.Literal)
            {
                var patchContent = patchScalar.Value ?? "";
                var replacer = new StrategicMergePatchImageReplacer(patchContent, defaultRegistry, log);
                var result = replacer.UpdateImages(imagesToUpdate);

                if (result.UpdatedImageReferences.Count > 0)
                {
                    patchScalar.Value = result.UpdatedContents;
                    allUpdatedImages.UnionWith(result.UpdatedImageReferences);
                }
            }
        }

        if (!allUpdatedImages.Any())
        {
            return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());   
        }

        using var writer = new StringWriter();
        yamlStream.Save(writer, false);
        var modifiedContent = writer.ToString().TrimEnd();

        return new ImageReplacementResult(modifiedContent, allUpdatedImages, new HashSet<string>());
    }

    ImageReplacementResult ProcessInlineJson6902Patches(string content, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var yamlStream = YamlStreamLoader.TryLoad(content, log, "inline JSON 6902 patches");
        if (yamlStream?.Documents.Count != 1 || !(yamlStream.Documents[0].RootNode is YamlMappingNode rootNode))
        {
            return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());
        }

        if (!rootNode.Children.TryGetValue(new YamlScalarNode("patchesJson6902"), out var patchesNode) || !(patchesNode is YamlSequenceNode patchSequence))
        {
            return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());
        }

        var allUpdatedImages = new HashSet<string>();

        foreach (var patchEntryNode in patchSequence.Children.OfType<YamlMappingNode>())
        {
            if (patchEntryNode.Children.TryGetValue(new YamlScalarNode("patch"), out var patchContentNode) && patchContentNode is YamlScalarNode patchScalar && patchScalar.Style == ScalarStyle.Literal)
            {
                var patchContent = patchScalar.Value ?? "";
                var replacer = new YamlJson6902PatchImageReplacer(patchContent, defaultRegistry, log);
                var result = replacer.UpdateImages(imagesToUpdate);

                if (result.UpdatedImageReferences.Count > 0)
                {
                    patchScalar.Value = result.UpdatedContents;
                    allUpdatedImages.UnionWith(result.UpdatedImageReferences);
                }
            }
        }

        if (!allUpdatedImages.Any())
        {
            return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());
        }
            

        using var writer = new StringWriter();
        yamlStream.Save(writer, false);
        var modifiedContent = writer.ToString().TrimEnd();

        return new ImageReplacementResult(modifiedContent, allUpdatedImages, new HashSet<string>());
    }
}
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

    public KustomizeContainerImageReplacer(string input, string defaultRegistry, ILog log)
    {
        this.input = input;
        this.defaultRegistry = defaultRegistry;
        this.log = log;
    }

    public ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        if (KustomizationValidator.IsKustomizationResource(input))
        {
            return UpdateImageTags(imagesToUpdate);
        }
        else
        {
            return UpdatePatchFields(imagesToUpdate);
        }
    }

    private ImageReplacementResult UpdateImageTags(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
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

        return new ImageReplacementResult(updatedContent, allUpdatedImages);
    }

    private ImageReplacementResult UpdatePatchFields(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var patchType = DeterminePatchTypeFromFile(input);

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
        else
        {
            log.Verbose($"Unable to determine patch type for content, no image updates will be performed");
            return new ImageReplacementResult(input, new HashSet<string>());
        }
    }
    

    internal PatchType? DeterminePatchTypeFromFile(string content)
    {
        if (KustomizationValidator.IsKustomizationResource(content))
            return null;

        if (IsJson6902PatchContent(content))
            return PatchType.Json6902;

        if (IsStrategicMergePatchContent(content))
            return PatchType.StrategicMerge;

        return null;
    }

    internal bool IsJson6902PatchContent(string content)
    {
        try
        {

            var trimmedContent = content.Trim();

            if (trimmedContent.StartsWith("[") || trimmedContent.StartsWith("-"))
            {

                var hasOpField = System.Text.RegularExpressions.Regex.IsMatch(content,
                    @"[""']?op[""']?\s*:\s*[""']?(add|remove|replace|move|copy|test)[""']?",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                var hasPathField = content.Contains("path") || content.Contains("\"path\"") || content.Contains("'path'");

                return hasOpField && hasPathField;
            }

            return false;
        }
        catch (Exception ex)
        {
            log.Verbose($"Error determining if content is JSON 6902 patch: {ex.Message}");
            return false;
        }
    }

    internal bool IsStrategicMergePatchContent(string content)
    {
        try
        {

            var hasKubernetesFields = System.Text.RegularExpressions.Regex.IsMatch(content,
                @"[""']?(apiVersion|kind|metadata|spec|data)[""']?\s*:",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var hasImageReferences = System.Text.RegularExpressions.Regex.IsMatch(content,
                @"[""']?(image|containers)[""']?\s*:",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return hasKubernetesFields || hasImageReferences;
        }
        catch (Exception ex)
        {
            log.Verbose($"Error determining if content is strategic merge patch: {ex.Message}");
            return false;
        }
    }


    internal bool HasInlinePatches(string content)
    {
        try
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(content));

            if (yamlStream.Documents.Count == 0)
            {
                return false;
            }

            var rootNode = yamlStream.Documents[0].RootNode;
            if (rootNode is not YamlMappingNode mappingNode)
            {
                return false;
            }

            return mappingNode.Children.ContainsKey(new YamlScalarNode("patches"));
        }
        catch (Exception ex)
        {
            log.Verbose($"Error checking for inline patches: {ex.Message}");
            return false;
        }
    }

    internal bool HasInlineStrategicMergePatches(string content)
    {
        try
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(content));

            if (yamlStream.Documents.Count == 0)
            {
                return false;
            }

            var rootNode = yamlStream.Documents[0].RootNode;
            if (rootNode is not YamlMappingNode mappingNode)
            {
                return false;
            }

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
        catch (Exception ex)
        {
            log.Verbose($"Error checking for inline strategic merge patches: {ex.Message}");
            return false;
        }
    }

    internal bool HasInlineJson6902Patches(string content)
    {
        try
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(content));

            if (yamlStream.Documents.Count == 0)
            {
                return false;
            }

            var rootNode = yamlStream.Documents[0].RootNode;
            if (rootNode is not YamlMappingNode mappingNode)
            {
                return false;
            }

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
        catch (Exception ex)
        {
            log.Verbose($"Error checking for inline JSON 6902 patches: {ex.Message}");
            return false;
        }
    }

    private ImageReplacementResult ProcessInlineStrategicMergePatches(string content, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        try
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(content));

            if (yamlStream.Documents.Count != 1 || !(yamlStream.Documents[0].RootNode is YamlMappingNode rootNode))
                return new ImageReplacementResult(content, new HashSet<string>());

            if (!rootNode.Children.TryGetValue(new YamlScalarNode("patchesStrategicMerge"), out var patchesNode) ||
                !(patchesNode is YamlSequenceNode patchSequence))
                return new ImageReplacementResult(content, new HashSet<string>());

            var allUpdatedImages = new HashSet<string>();
            var hasChanges = false;

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
                        hasChanges = true;
                    }
                }
            }

            if (!hasChanges)
                return new ImageReplacementResult(content, new HashSet<string>());

            using var writer = new StringWriter();
            yamlStream.Save(writer, false);
            var modifiedContent = writer.ToString().TrimEnd();

            return new ImageReplacementResult(modifiedContent, allUpdatedImages);
        }
        catch (Exception ex)
        {
            log.WarnFormat("Error processing inline strategic merge patches: {0}", ex.Message);
            return new ImageReplacementResult(content, new HashSet<string>());
        }
    }

    private ImageReplacementResult ProcessInlineJson6902Patches(string content, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        try
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(content));

            if (yamlStream.Documents.Count != 1 || !(yamlStream.Documents[0].RootNode is YamlMappingNode rootNode))
                return new ImageReplacementResult(content, new HashSet<string>());

            if (!rootNode.Children.TryGetValue(new YamlScalarNode("patchesJson6902"), out var patchesNode) ||
                !(patchesNode is YamlSequenceNode patchSequence))
                return new ImageReplacementResult(content, new HashSet<string>());

            var allUpdatedImages = new HashSet<string>();
            var hasChanges = false;

            foreach (var patchEntryNode in patchSequence.Children.OfType<YamlMappingNode>())
            {
                if (patchEntryNode.Children.TryGetValue(new YamlScalarNode("patch"), out var patchContentNode) &&
                    patchContentNode is YamlScalarNode patchScalar &&
                    patchScalar.Style == ScalarStyle.Literal)
                {
                    var patchContent = patchScalar.Value ?? "";
                    var replacer = new YamlJson6902PatchImageReplacer(patchContent, defaultRegistry, log);
                    var result = replacer.UpdateImages(imagesToUpdate);

                    if (result.UpdatedImageReferences.Count > 0)
                    {
                        patchScalar.Value = result.UpdatedContents;
                        allUpdatedImages.UnionWith(result.UpdatedImageReferences);
                        hasChanges = true;
                    }
                }
            }

            if (!hasChanges)
                return new ImageReplacementResult(content, new HashSet<string>());

            using var writer = new StringWriter();
            yamlStream.Save(writer, false);
            var modifiedContent = writer.ToString().TrimEnd();

            return new ImageReplacementResult(modifiedContent, allUpdatedImages);
        }
        catch (Exception ex)
        {
            log.WarnFormat("Error processing inline JSON 6902 patches: {0}", ex.Message);
            return new ImageReplacementResult(content, new HashSet<string>());
        }
    }
}
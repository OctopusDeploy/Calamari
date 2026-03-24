using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Patching;
using Calamari.Kubernetes.Patching.JsonPatch;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class KustomizeUpdater : BaseUpdater
{
    readonly IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate;
    readonly string defaultRegistry;

    private string currentFilePath = "";

    public KustomizeUpdater(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
                            string defaultRegistry,
                            ILog log,
                            ICalamariFileSystem fileSystem) : base(log,
                                                                   fileSystem)
    {
        this.imagesToUpdate = imagesToUpdate;
        this.defaultRegistry = defaultRegistry;
    }

    public override ImageReplacementResult ReplaceImages(string input)
    {
        var updatedContent = input;
        var allUpdatedImages = new HashSet<string>();

        if (IsKustomizationFile(currentFilePath))
        {
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
        }
        else
        {
            var patchType = DeterminePatchTypeFromFile(input, currentFilePath);

            IContainerImageReplacer replacer = patchType switch
            {
                PatchType.StrategicMerge => new StrategicMergePatchImageReplacer(input, defaultRegistry, log),
                PatchType.Json6902 => new JsonPatchImageReplacer(input, defaultRegistry, log),
                _ => null
            };

            if (replacer != null)
            {
                var result = replacer.UpdateImages(imagesToUpdate);
                if (result.UpdatedImageReferences.Count > 0)
                {
                    updatedContent = result.UpdatedContents;
                    allUpdatedImages.UnionWith(result.UpdatedImageReferences);
                }
            }
        }

        return new ImageReplacementResult(updatedContent, allUpdatedImages);
    }


    internal static PatchType? DeterminePatchTypeFromFile(string content, string filePath)
    {
        // Kustomization files are not patch files
        if (IsKustomizationFile(filePath))
            return null;

        // Check if it's a YAML/JSON file first (only these can be patch files)
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension != ".yaml" && extension != ".yml" && extension != ".json")
            return null;

        // Check for JSON 6902 patch pattern first (more specific)
        if (IsJson6902PatchContent(content))
            return PatchType.Json6902;

        // Check for strategic merge patch pattern
        if (IsStrategicMergePatchContent(content))
            return PatchType.StrategicMerge;

        return null;
    }

    internal static bool IsJson6902PatchContent(string content)
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
        catch
        {
            return false;
        }
    }

    internal static bool IsStrategicMergePatchContent(string content)
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
        catch
        {
            return false;
        }
    }

    internal static bool IsKustomizationFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        return fileName == "kustomization.yaml" || fileName == "kustomization.yml";
    }

    internal static bool HasInlinePatches(string content)
    {
        try
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(content));

            if (yamlStream.Documents.Count == 0)
                return false;

            var rootNode = yamlStream.Documents[0].RootNode;
            if (rootNode is not YamlMappingNode mappingNode)
                return false;

            return mappingNode.Children.ContainsKey(new YamlScalarNode("patches"));
        }
        catch
        {
            return false;
        }
    }

    internal static bool HasInlineStrategicMergePatches(string content)
    {
        try
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(content));

            if (yamlStream.Documents.Count == 0)
                return false;

            var rootNode = yamlStream.Documents[0].RootNode;
            if (rootNode is not YamlMappingNode mappingNode)
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
        catch
        {
            return false;
        }
    }

    internal static bool HasInlineJson6902Patches(string content)
    {
        try
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(content));

            if (yamlStream.Documents.Count == 0)
                return false;

            var rootNode = yamlStream.Documents[0].RootNode;
            if (rootNode is not YamlMappingNode mappingNode)
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
        catch
        {
            return false;
        }
    }

    internal ImageReplacementResult ProcessInlineStrategicMergePatches(string content, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
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

            // Process each inline patch in the sequence
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

    internal ImageReplacementResult ProcessInlineJson6902Patches(string content, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
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

    protected new FileUpdateResult Update(string rootPath, HashSet<string> filesToUpdate)
    {
        var updatedImages = new HashSet<string>();
        var jsonPatches = new List<FilePathContent>();

        foreach (var file in filesToUpdate)
        {
            currentFilePath = file;
            var relativePath = Path.GetRelativePath(rootPath, file);
            log.Verbose($"Processing file {relativePath}.");
            var content = fileSystem.ReadFile(file);

            var imageReplacementResult = ReplaceImages(content);

            if (imageReplacementResult.UpdatedImageReferences.Count > 0)
            {
                // Replace \ with / so that Calamari running on windows doesn't cause issues when we send back to server
                jsonPatches.Add(new(relativePath.Replace('\\', '/'), Serialize(CreateJsonPatch(content, imageReplacementResult.UpdatedContents))));
                fileSystem.OverwriteFile(file, imageReplacementResult.UpdatedContents);
                updatedImages.UnionWith(imageReplacementResult.UpdatedImageReferences);
                log.Verbose($"Updating file {relativePath} with new image references.");
                foreach (var change in imageReplacementResult.UpdatedImageReferences)
                {
                    log.Verbose($"Updated image reference: {change}");
                }
            }
            else
            {
                log.Verbose($"No changes made to file {relativePath} as no image references were updated.");
            }
        }

        currentFilePath = ""; // Reset after processing
        return new FileUpdateResult(updatedImages, jsonPatches, []);
    }

    private static new JsonPatchDocument CreateJsonPatch(string originalContent, string updatedContent)
    {
        var originalStream = new YamlStream();
        originalStream.Load(new StringReader(originalContent));
        var original = new JsonArray(originalStream.Documents.Select(d => d.ToJsonNode()).ToArray());

        var updatedStream = new YamlStream();
        updatedStream.Load(new StringReader(updatedContent));
        var updated = new JsonArray(updatedStream.Documents.Select(d => d.ToJsonNode()).ToArray());

        return JsonPatchGenerator.Generate(original, updated);
    }

    private static string Serialize(JsonPatchDocument patchDocument)
    {
        return JsonSerializer.Serialize(patchDocument);
    }
 
    public override FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        var applicationSource = sourceWithMetadata.Source;

        if (applicationSource.Path == null)
        {
            log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
            return new FileUpdateResult( [], [], []);
        }

        log.Verbose($"Reading files from {applicationSource.Path}");

        return ProcessKustomizeSource(workingDirectory, applicationSource.Path!);
    }

    FileUpdateResult ProcessKustomizeSource(
        string rootPath,
        string subFolder)
    {
        var absSubFolder = Path.Combine(rootPath, subFolder);

        var kustomizationFile = KustomizeDiscovery.TryFindKustomizationFile(fileSystem, absSubFolder);
        if (kustomizationFile == null)
        {
            log.Warn("kustomization file not found, no files will be updated");
            return new FileUpdateResult([], [], []);
        }

        log.Verbose("kustomization file found, processing images and discovering patch files");

        var allFilesToUpdate = new HashSet<string> { kustomizationFile };
        var patchFiles = KustomizePatchDiscovery.DiscoverPatchFiles(fileSystem, kustomizationFile, log);

        var externalPatchFiles = patchFiles
                                 .Where(p => p.Type != PatchType.InlineJsonPatch)
                                 .Select(p => p.FilePath)
                                 .Where(fileSystem.FileExists);

        allFilesToUpdate.UnionWith(externalPatchFiles);

        log.VerboseFormat("Processing {0} files total (kustomization + {1} external patch files)",
                          allFilesToUpdate.Count, externalPatchFiles.Count());

        return Update(rootPath, allFilesToUpdate);
    }

}
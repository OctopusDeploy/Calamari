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

    // 1. Determine what file type it is
    // 1a. If it is a Kustomize file, run KustomizeImageReplace(which will handle the normal update of image tags + inline patches)
    // 1b. If it not a Kustomize file, run UpdatePatchWithImages(input, imagesToUpdate, defaultRegistry, log)
    // UpdatePatchWithImages will be responsible for determining the patch type of the input and then call type specific update
    public override ImageReplacementResult ReplaceImages(string input)
    {
        var updatedContent = input;
        var allUpdatedImages = new HashSet<string>();

        // For kustomization files, we may need to chain replacers (images + inline patches)
        if (IsKustomizationFile(currentFilePath))
        {
            // Always process images field first
            var kustomizeReplacer = new KustomizeImageReplacer(updatedContent, defaultRegistry, log);
            var result = kustomizeReplacer.UpdateImages(imagesToUpdate);

            if (result.UpdatedImageReferences.Count > 0)
            {
                updatedContent = result.UpdatedContents;
                allUpdatedImages.UnionWith(result.UpdatedImageReferences);
            }

            // Then process inline patches if present
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
        }
        else
        {
            // For external patch files, determine patch type and use appropriate replacer
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
        // For kustomization files, return null (handled separately)
        if (IsKustomizationFile(filePath))
            return null;

        // Try to determine patch type based on content structure first (more accurate than file extension)
        if (IsJson6902PatchContent(content))
            return PatchType.Json6902;

        // If not JSON 6902, check if it looks like a Kubernetes resource (strategic merge)
        if (IsStrategicMergePatchContent(content))
            return PatchType.StrategicMerge;

        // Fallback to extension-based detection for edge cases
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".json" || extension == ".yaml" || extension == ".yml")
            return PatchType.StrategicMerge; // Default to strategic merge as it's more common

        return null;
    }

    internal static bool IsJson6902PatchContent(string content)
    {
        try
        {
            // JSON 6902 patches are arrays of operation objects
            // Look for the characteristic structure: array with objects containing "op" field
            var trimmedContent = content.Trim();

            // Must start with array bracket (could be JSON or YAML array)
            if (trimmedContent.StartsWith("[") || trimmedContent.StartsWith("-"))
            {
                // Look for JSON 6902 operation patterns with more precise matching
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
            // Strategic merge patches typically have Kubernetes resource structure
            // Look for common Kubernetes fields - handle both YAML and JSON formats
            var hasKubernetesFields = System.Text.RegularExpressions.Regex.IsMatch(content,
                @"[""']?(apiVersion|kind|metadata|spec|data)[""']?\s*:",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Or contains image references (common in patches for image updates)
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

            // Check for 'patches' field in the kustomization file
            return mappingNode.Children.ContainsKey(new YamlScalarNode("patches"));
        }
        catch
        {
            // If we can't parse the YAML, assume no inline patches
            return false;
        }
    }
    
    protected new FileUpdateResult Update(string rootPath, HashSet<string> filesToUpdate)
    {
        var updatedImages = new HashSet<string>();
        var jsonPatches = new List<FilePathContent>();

        foreach (var file in filesToUpdate)
        {
            currentFilePath = file; // Track current file being processed
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
        return new FileUpdateResult(updatedImages, jsonPatches);
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
            return new FileUpdateResult( [], []);
        }

        log.Verbose($"Reading files from {applicationSource.Path}");

        // rename to KustomizeSource 
        return ProcessKustomizeApplication(workingDirectory, applicationSource.Path!);
    }
    
    // 1. Finds the Kustomize file (existing)
    // 2. Finds all referenced external patched files. For example, if the kustomization file has the following patch field:
    // patches:
    // - path: increase_replicas.yaml
    // - path: set_memory.yaml
    // another example
    // target:
    //    group: apps
    //    version: v1
    //    kind: Deployment
    //    name: my-nginx
    //  path: patch.yaml
    // we want to collate the external files.
    // 2a. filter out the patch files not relevant to updating image tags (e.g config maps won't have any image tags to update)
    // 3. Call the Update() function with a list of the files to update
    FileUpdateResult ProcessKustomizeApplication(
        string rootPath,
        string subFolder)
    {
        var absSubFolder = Path.Combine(rootPath, subFolder);

        // 1. Find the Kustomize file
        var kustomizationFile = KustomizeDiscovery.TryFindKustomizationFile(fileSystem, absSubFolder);
        if (kustomizationFile == null)
        {
            log.Warn("kustomization file not found, no files will be updated");
            return new FileUpdateResult([], []);
        }

        log.Verbose("kustomization file found, processing images and discovering patch files");

        // 2. Find all referenced external patch files
        var allFilesToUpdate = new HashSet<string> { kustomizationFile };
        var patchFiles = KustomizePatchDiscovery.DiscoverPatchFiles(fileSystem, kustomizationFile, log);

        // 2a. Filter out patch files not relevant to updating image tags and add external patch files
        var externalPatchFiles = patchFiles
            .Where(p => p.Type != PatchType.InlineJsonPatch) // Exclude inline patches (they're in kustomization.yaml)
            .Select(p => p.FilePath)
            .Where(fileSystem.FileExists);

        allFilesToUpdate.UnionWith(externalPatchFiles);

        log.VerboseFormat("Processing {0} files total (kustomization + {1} external patch files)",
            allFilesToUpdate.Count, externalPatchFiles.Count());

        // 3. Call the Update() function with a list of all files to update
        return Update(rootPath, allFilesToUpdate);
    }

}
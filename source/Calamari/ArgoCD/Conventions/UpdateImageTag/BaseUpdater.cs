using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Patching;
using Calamari.Kubernetes.Patching.JsonPatch;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public interface ISourceUpdater
{
    public FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory);
}

public class NoOpSourceUpdater : ISourceUpdater
{
    public FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory) => new( [], [], [], []);
}

public abstract class BaseUpdater : ISourceUpdater
{
    protected readonly ILog log;
    protected readonly ICalamariFileSystem fileSystem;

    protected BaseUpdater(ILog log,
                          ICalamariFileSystem fileSystem)
    {
        this.log = log;
        this.fileSystem = fileSystem;
    }

    public abstract ImageReplacementResult ReplaceImages(string input);

    protected FileUpdateResult Update(string rootPath, HashSet<string> filesToUpdate)
    {
        var updatedImages = new HashSet<string>();
        var jsonPatches = new List<FileJsonPatch>();
        foreach (var file in filesToUpdate)
        {
            var relativePath = Path.GetRelativePath(rootPath, file);
            log.Verbose($"Processing file {relativePath}.");
            var content = fileSystem.ReadFile(file);

            var imageReplacementResult = ReplaceImages(content);

            if (imageReplacementResult.UpdatedImageReferences.Count > 0)
            {
                fileSystem.OverwriteFile(file, imageReplacementResult.UpdatedContents);
                updatedImages.UnionWith(imageReplacementResult.UpdatedImageReferences);
                log.Verbose($"Updating file {relativePath} with new image references.");
                foreach (var change in imageReplacementResult.UpdatedImageReferences)
                {
                    log.Verbose($"Updated image reference: {change}");
                }

                if (imageReplacementResult.AlreadyUpToDateImages.Count > 0)
                {
                    // Mixed case: some images were updated, others were already at the target tag.
                    // Build the patch from the updated content treating ALL targeted images
                    // (both just-updated and already-correct) as needing a patch, so that the
                    // server can verify the desired tag for every container that references them.
                    var allTargetedImages = new HashSet<string>(imageReplacementResult.AlreadyUpToDateImages);
                    allTargetedImages.UnionWith(imageReplacementResult.UpdatedImageReferences);
                    var patch = CreateNoOpJsonPatch(imageReplacementResult.UpdatedContents, allTargetedImages, ReplaceImages);
                    if (patch != null)
                    {
                        jsonPatches.Add(new(relativePath, Serialize(patch)));
                    }
                    foreach (var alreadyUpToDate in imageReplacementResult.AlreadyUpToDateImages)
                    {
                        log.Verbose($"Image reference already up-to-date: {alreadyUpToDate}");
                    }
                }
                else
                {
                    jsonPatches.Add(new(relativePath, Serialize(CreateJsonPatch(content, imageReplacementResult.UpdatedContents))));
                }
            }
            else if (imageReplacementResult.AlreadyUpToDateImages.Count > 0)
            {
                // Image reference was found but the tag is already at the target value:
                // generate a temporary patch representing the change we would have made.
                // This allows the server to verify the specific image tag without being
                // sensitive to unrelated file changes.
                var patch = CreateNoOpJsonPatch(content, imageReplacementResult.AlreadyUpToDateImages, ReplaceImages);
                if (patch != null)
                {
                    jsonPatches.Add(new(relativePath, Serialize(patch)));
                }
                log.Verbose($"No changes made to file {relativePath} — image references are already up-to-date.");
            }
            else
            {
                log.Verbose($"No changes made to file {relativePath} as no image references were updated.");
            }
        }

        return new FileUpdateResult(updatedImages, [], jsonPatches, []);
    }

    /// <summary>
    /// Creates a version of the content with the already-correct image tags replaced by a placeholder.
    /// Running the image replacer on this temporary content produces the correct content, and the diff
    /// between the two gives a meaningful patch that only targets the specific image tag fields.
    /// </summary>
    protected static string CreateTemporaryBeforeContent(string content, HashSet<string> targetedImages)
    {
        var temporaryBefore = content;
        foreach (var imageRef in targetedImages)
        {
            var colonIdx = imageRef.LastIndexOf(':');
            if (colonIdx >= 0)
            {
                temporaryBefore = temporaryBefore.Replace(imageRef, imageRef[..colonIdx] + ":__CALAMARI_PLACEHOLDER__");
            }
        }
        return temporaryBefore;
    }

    /// <summary>
    /// Generates a JSON patch representing what the image update *would* have done, for cases where
    /// the image tag is already at the target value. Returns null if no patch could be produced.
    /// </summary>
    protected JsonPatchDocument? CreateNoOpJsonPatch(string content, HashSet<string> targetedImages, Func<string, ImageReplacementResult> replacer)
    {
        var temporaryBefore = CreateTemporaryBeforeContent(content, targetedImages);
        var temporaryResult = replacer(temporaryBefore);
        return temporaryResult.UpdatedImageReferences.Count > 0
            ? CreateJsonPatch(temporaryBefore, temporaryResult.UpdatedContents)
            : null;
    }

    protected static JsonPatchDocument CreateJsonPatch(string originalContent, string updatedContent)
    {
        var originalStream = new YamlStream();
        originalStream.Load(new StringReader(originalContent));
        var original = new JsonArray(originalStream.Documents.Select(d => d.ToJsonNode()).ToArray());

        var updatedStream = new YamlStream();
        updatedStream.Load(new StringReader(updatedContent));
        var updated = new JsonArray(updatedStream.Documents.Select(d => d.ToJsonNode()).ToArray());

        return JsonPatchGenerator.Generate(original, updated);
    }

    static string Serialize(JsonPatchDocument patchDocument)
    {
        return JsonSerializer.Serialize(patchDocument);
    }

    public abstract FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory);
}
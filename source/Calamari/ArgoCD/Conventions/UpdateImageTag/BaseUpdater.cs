#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Patching.JsonPatch;

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

            var allTargetedImages = new HashSet<string>(imageReplacementResult.UpdatedImageReferences);
            allTargetedImages.UnionWith(imageReplacementResult.AlreadyUpToDateImages);

            foreach (var unrecognisedKind in imageReplacementResult.UnrecognisedKinds)
            {
                log.WarnFormat("Type '{0}' is not recognised by the Image Update step. Images on this type will not be updated.", unrecognisedKind);
            }

            if (imageReplacementResult.UpdatedImageReferences.Count > 0)
            {
                fileSystem.OverwriteFile(file, imageReplacementResult.UpdatedContents);
                updatedImages.UnionWith(imageReplacementResult.UpdatedImageReferences);
                log.Verbose($"Updating file {relativePath} with new image references.");
                foreach (var change in imageReplacementResult.UpdatedImageReferences)
                {
                    log.Verbose($"Updated image reference: {change}");
                }
                foreach (var alreadyUpToDate in imageReplacementResult.AlreadyUpToDateImages)
                {
                    log.Verbose($"Image reference already up-to-date: {alreadyUpToDate}");
                }
            }
            else if (allTargetedImages.Count > 0)
            {
                log.Verbose($"No changes made to file {relativePath} — image references are already up-to-date.");
            }
            else
            {
                log.Verbose($"No changes made to file {relativePath} as no image references were updated.");
            }

            if (allTargetedImages.Count > 0)
            {
                var patch = CreateJsonPatch(content, allTargetedImages);
                if (patch != null)
                {
                    jsonPatches.Add(new(relativePath, Serialize(patch)));
                }
            }
        }

        return new FileUpdateResult(updatedImages, [], jsonPatches, []);
    }

    /// <summary>
    /// Generates a JSON patch representing the desired state for each targeted image,
    /// whether or not it was actually updated. Returns null if no patch could be produced.
    /// </summary>
    protected virtual JsonPatchDocument? CreateJsonPatch(string content, HashSet<string> targetedImages)
    {
        var temporaryBefore = content;
        foreach (var imageRef in targetedImages)
        {
            var colonIdx = imageRef.LastIndexOf(':');
            if (colonIdx >= 0)
            {
                temporaryBefore = temporaryBefore.Replace(imageRef, JsonPatchUtils.MakePlaceholderRef(imageRef));
            }
        }

        var temporaryResult = ReplaceImages(temporaryBefore);
        return temporaryResult.UpdatedImageReferences.Count > 0
            ? JsonPatchUtils.CreateJsonPatchFromDiff(temporaryBefore, temporaryResult.UpdatedContents)
            : null;
    }

    static string Serialize(JsonPatchDocument patchDocument)
    {
        return JsonSerializer.Serialize(patchDocument);
    }

    public abstract FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory);
}
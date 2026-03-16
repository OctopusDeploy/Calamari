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
    public FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory) => new([], []);
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
        var jsonPatches = new List<FilePathContent>();
        foreach (var file in filesToUpdate)
        {
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

        return new FileUpdateResult(updatedImages, jsonPatches);
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

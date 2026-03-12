using System;
using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public interface ISourceUpdater
{
    public FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory);
}

public abstract class BaseUpdater : ISourceUpdater
{
    protected readonly ILog log;
    protected readonly ICalamariFileSystem fileSystem;

    protected BaseUpdater( ILog log,
                          ICalamariFileSystem fileSystem)
    {
        this.log = log;
        this.fileSystem = fileSystem;
    }

    protected FileUpdateResult Update(string rootPath, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> filesToUpdate, Func<string, IContainerImageReplacer> imageReplacerFactory)
    {
        var updatedFiles = new HashSet<string>();
        var updatedImages = new HashSet<string>();
        var jsonPatches = new List<FilePathContent>();
        foreach (var file in filesToUpdate)
        {
            var relativePath = Path.GetRelativePath(rootPath, file);
            log.Verbose($"Processing file {relativePath}.");
            var content = fileSystem.ReadFile(file);

            var imageReplacer = imageReplacerFactory(content);
            var imageReplacementResult = imageReplacer.UpdateImages(imagesToUpdate);

            if (imageReplacementResult.UpdatedImageReferences.Count > 0)
            {
                // Replace \ with / so that Calamari running on windows doesn't cause issues when we send back to server
                jsonPatches.Add(new(relativePath.Replace('\\', '/'), UpdaterHelpers.Serialize(UpdaterHelpers.CreateJsonPatch(content, imageReplacementResult.UpdatedContents))));
                fileSystem.OverwriteFile(file, imageReplacementResult.UpdatedContents);
                updatedImages.UnionWith(imageReplacementResult.UpdatedImageReferences);
                updatedFiles.Add(relativePath);
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

        return new FileUpdateResult(updatedFiles, updatedImages, jsonPatches);
    }

    public abstract FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory);
}
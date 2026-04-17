using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class KustomizeUpdater : BaseUpdater
{
    readonly IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate;
    readonly string defaultRegistry;
    readonly bool updateKustomizePatches;

    public KustomizeUpdater(UpdateArgoCDAppDeploymentConfig deploymentConfig,
                            string defaultRegistry,
                            ILog log,
                            ICalamariFileSystem fileSystem) : base(log,
                                                                   fileSystem)
    {
        imagesToUpdate = deploymentConfig.ImageReferences;
        updateKustomizePatches = deploymentConfig.UpdateKustomizePatches;
        this.defaultRegistry = defaultRegistry;
    }

    public override ImageReplacementResult ReplaceImages(string input)
    {
        var imageReplacer = new KustomizeContainerImageReplacer(input, defaultRegistry, updateKustomizePatches, log);
        return imageReplacer.UpdateImages(imagesToUpdate);
    }

    protected override string CreateTemporaryBeforeContent(string content, HashSet<string> targetedImages)
    {
        // For kustomization resources, name and tag are separate YAML fields (name + newTag),
        // so the base class's string replacement of "name:tag" won't find a match.
        // Instead, run the existing replacer with placeholder tags to produce the "before" content.
        if (!KustomizationValidator.IsKustomizationResource(content))
        {
            return base.CreateTemporaryBeforeContent(content, targetedImages);
        }

        var placeholderImages = targetedImages
                                .Select(imageRef =>
                                {
                                    var colonIdx = imageRef.LastIndexOf(':');
                                    var placeholderRef = colonIdx >= 0
                                        ? imageRef[..colonIdx] + ":__CALAMARI_PLACEHOLDER__"
                                        : imageRef;
                                    return new ContainerImageReferenceAndHelmReference(
                                        ContainerImageReference.FromReferenceString(placeholderRef, defaultRegistry));
                                })
                                .ToList();

        var replacer = new KustomizeContainerImageReplacer(content, defaultRegistry, updateKustomizePatches, log);
        var result = replacer.UpdateImages(placeholderImages);

        return result.UpdatedContents;
    }

    public override FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        var applicationSource = sourceWithMetadata.Source;

        if (applicationSource.Path == null)
        {
            log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
            return new FileUpdateResult([], [], [], []);
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
            return new FileUpdateResult([], [], [], []);
        }

        var allFilesToUpdate = new HashSet<string> { kustomizationFile };

        if (updateKustomizePatches)
        {
            log.Verbose("kustomization file found, processing images and discovering patch files");

            var patchDiscovery = new KustomizePatchDiscovery(fileSystem, log);
            var patchFiles = patchDiscovery.DiscoverPatch(kustomizationFile);

            var externalPatchFiles = patchFiles
                                     .Where(p => p.Type != PatchType.InlineJsonPatch)
                                     .Select(p => p.FilePath)
                                     .Where(fileSystem.FileExists)
                                     .ToList();

            allFilesToUpdate.UnionWith(externalPatchFiles);

            log.VerboseFormat("Processing {0} files total (kustomization + {1} external patch files)",
                              allFilesToUpdate.Count, externalPatchFiles.Count);
        }

        return Update(rootPath, allFilesToUpdate);
    }

}
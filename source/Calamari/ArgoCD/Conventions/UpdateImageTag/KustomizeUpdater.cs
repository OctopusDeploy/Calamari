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

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class KustomizeUpdater : BaseUpdater
{
    readonly IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate;
    readonly string defaultRegistry;

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
        var imageReplacer = new KustomizeContainerImageReplacer(input, defaultRegistry, log);
        return imageReplacer.UpdateImages(imagesToUpdate);
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

        log.Verbose("kustomization file found, processing images and discovering patch files");

        var allFilesToUpdate = new HashSet<string> { kustomizationFile };
        var patchDiscovery = new KustomizePatchDiscovery(fileSystem, log);
        var patchFiles = patchDiscovery.DiscoverPatchFiles(kustomizationFile);

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
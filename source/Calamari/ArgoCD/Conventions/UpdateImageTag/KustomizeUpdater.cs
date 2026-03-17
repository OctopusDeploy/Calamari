using System;
using System.Collections.Generic;
using System.IO;
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
        var imageReplacer = new KustomizeImageReplacer(input, defaultRegistry, log);
        return imageReplacer.UpdateImages(imagesToUpdate);
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

        var results = new List<FileUpdateResult>();

        results.Add(UpdateKustomizeYaml(workingDirectory, applicationSource.Path!));

        results.Add(UpdatePatchFiles(workingDirectory, applicationSource.Path!));

        return CombineResults(results);
    }
    
    FileUpdateResult UpdateKustomizeYaml(
        string rootPath,
        string subFolder)
    {
        var absSubFolder = Path.Combine(rootPath, subFolder);

        var kustomizationFile = KustomizeDiscovery.TryFindKustomizationFile(fileSystem, absSubFolder);
        if (kustomizationFile != null)
        {
            var filesToUpdate = new HashSet<string> { kustomizationFile };
            log.Verbose("kustomization file found, will only update images transformer in the kustomization file");
            return Update(rootPath, filesToUpdate);
        }

        log.Warn("kustomization file not found, no files will be updated");
        return new FileUpdateResult( [], []);
    }

    FileUpdateResult UpdatePatchFiles(string rootPath, string subFolder)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path cannot be null or empty", nameof(rootPath));

        if (string.IsNullOrWhiteSpace(subFolder))
            throw new ArgumentException("Sub folder cannot be null or empty", nameof(subFolder));

        var absSubFolder = Path.Combine(rootPath, subFolder);

        if (!fileSystem.DirectoryExists(absSubFolder))
        {
            log.WarnFormat("Directory does not exist: {0}", absSubFolder);
            return new FileUpdateResult([], []);
        }

        var kustomizationFile = KustomizeDiscovery.TryFindKustomizationFile(fileSystem, absSubFolder);

        if (kustomizationFile == null)
        {
            log.Verbose("No kustomization file found for patch processing");
            return new FileUpdateResult([], []);
        }

        var patchFiles = KustomizePatchDiscovery.DiscoverPatchFiles(fileSystem, kustomizationFile, log);
        if (patchFiles.Count == 0)
        {
            log.Verbose("No patch files found in kustomization file");
            return new FileUpdateResult([], []);
        }

        log.VerboseFormat("Found {0} patch files to process", patchFiles.Count);

        var results = new List<FileUpdateResult>();

        foreach (var patchFile in patchFiles)
        {
            try
            {
                var updater = CreateUpdaterForPatchType(patchFile);
                var result = updater.ProcessPatchFiles(rootPath, subFolder);
                results.Add(result);
            }
            catch (Exception ex)
            {
                log.WarnFormat("Error processing patch file {0}: {1}", patchFile.FilePath, ex.Message);
            }
        }

        return CombineResults(results);
    }

    BasePatchUpdater CreateUpdaterForPatchType(PatchFileInfo patchFile)
    {
        return patchFile.Type switch
        {
            PatchType.StrategicMerge => new StrategicMergePatchUpdater(imagesToUpdate, defaultRegistry, patchFile.FilePath, log, fileSystem),
            PatchType.Json6902 => new JsonPatchUpdater(imagesToUpdate, defaultRegistry, patchFile.FilePath, log, fileSystem),
            PatchType.InlineJsonPatch => new InlineJsonPatchUpdater(imagesToUpdate, defaultRegistry, patchFile.FilePath, log, fileSystem),
            _ => throw new ArgumentOutOfRangeException($"Unknown patch type: {patchFile.Type}")
        };
    }

    static FileUpdateResult CombineResults(List<FileUpdateResult> results)
    {
        var allUpdatedImages = new HashSet<string>();
        var allJsonPatches = new List<FilePathContent>();

        foreach (var result in results)
        {
            allUpdatedImages.UnionWith(result.UpdatedImages);
            allJsonPatches.AddRange(result.PatchedFileContent);
        }

        return new FileUpdateResult(allUpdatedImages, allJsonPatches);
    }
}
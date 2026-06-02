using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Patching.JsonPatch;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public abstract class AbstractHelmUpdater : BaseUpdater
{
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly string defaultRegistry;

    protected AbstractHelmUpdater(ILog log,
                                  ICalamariFileSystem fileSystem,
                                  UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                  string defaultRegistry) : base(log,
                                                                 fileSystem)
    {
        this.deploymentConfig = deploymentConfig;
        this.defaultRegistry = defaultRegistry;
    }
    
    public override ImageReplacementResult ReplaceImages(string input)
    {
        var imageReplacer = new HelmValuesImageReplaceStepVariables(input, defaultRegistry, log);
        return imageReplacer.UpdateImages(deploymentConfig.ImageReferences);
    }

    protected override JsonPatchDocument? CreateJsonPatch(string content, HashSet<string> targetedImages)
    {
        return CreateJsonPatchWithPlaceholders(content, deploymentConfig.ImageReferences,
            (c, images) => new HelmValuesImageReplaceStepVariables(c, defaultRegistry, log).UpdateImages(images));
    }

    //NOTE: this is common with Helm Sources
    protected FileUpdateResult ProcessHelmValuesFiles(HashSet<string> filesToUpdate,
                                                      string workingDirectory,
                                                      ApplicationSourceWithMetadata sourceWithMetadata)
    {
        log.Verbose($"Found {filesToUpdate.Count} yaml files to process");
        return Update(workingDirectory, filesToUpdate.ToHashSet());
    }

    /// <returns>Images that were updated</returns>
    protected FileUpdateResult ProcessHelmUpdateTargets(
        string workingDirectory,
        IReadOnlyCollection<HelmValuesFileImageUpdateTarget> targets)
    {
        var results =
            targets.Select(t => UpdateHelmImageValues(workingDirectory, t, deploymentConfig.ImageReferences))
                   .ToList();

        var patchedFiles = results
            .Where(r => r.JsonPatch != null)
            .Select(r => new FileJsonPatch(r.RelativeFilepath, JsonSerializer.Serialize(r.JsonPatch)))
            .ToList();
        var updatedImages = results
            .Where(r => r.Updated)
            .SelectMany(r => r.ImagesUpdated)
            .ToHashSet();

        return new FileUpdateResult(updatedImages, [], patchedFiles, []);
    }

    HelmRefUpdatedResult UpdateHelmImageValues(
        string rootPath,
        HelmValuesFileImageUpdateTarget target,
        IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var filepath = Path.Combine(rootPath, target.Path, target.FileName);
        log.Info($"Processing file at {filepath}.");
        var fileContent = fileSystem.ReadFile(filepath);
        var helmImageReplacer = new HelmContainerImageReplacer(fileContent, target.DefaultClusterRegistry, target.ImagePathDefinitions, log);
        var imageUpdateResult = helmImageReplacer.UpdateImages(imagesToUpdate);

        if (imageUpdateResult.UpdatedImageReferences.Count > 0)
            fileSystem.OverwriteFile(filepath, imageUpdateResult.UpdatedContents);

        var jsonPatch = CreateJsonPatchWithPlaceholders(fileContent, imagesToUpdate,
            (c, images) => new HelmContainerImageReplacer(c, target.DefaultClusterRegistry, target.ImagePathDefinitions, log).UpdateImages(images));

        return new HelmRefUpdatedResult(imageUpdateResult.UpdatedImageReferences, Path.Combine(target.Path, target.FileName), jsonPatch);
    }

    /// <summary>
    /// Creates a JSON patch by running a replacer factory with placeholder tags to produce a "before",
    /// then running with real tags against the "before" to produce an "after", and diffing the two.
    /// </summary>
    JsonPatchDocument? CreateJsonPatchWithPlaceholders(
        string content,
        IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
        Func<string, IReadOnlyCollection<ContainerImageReferenceAndHelmReference>, ImageReplacementResult> replacerFactory)
    {
        var placeholderImages = imagesToUpdate
            .Select(ir =>
            {
                var placeholderRef = MakePlaceholderRef(ir.ContainerReference.FriendlyName());
                return ir with { ContainerReference = ContainerImageReference.FromReferenceString(placeholderRef, defaultRegistry) };
            })
            .ToList();

        var placeholderResult = replacerFactory(content, placeholderImages);
        if (placeholderResult.UpdatedImageReferences.Count == 0)
            return null;

        var temporaryBefore = placeholderResult.UpdatedContents;
        var actualResult = replacerFactory(temporaryBefore, imagesToUpdate);

        return actualResult.UpdatedImageReferences.Count > 0
            ? CreateJsonPatchFromDiff(temporaryBefore, actualResult.UpdatedContents)
            : null;
    }
}

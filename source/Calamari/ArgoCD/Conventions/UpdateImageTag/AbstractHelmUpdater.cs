using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        var imageReplacer = new HelmContainerImageReplacer(input, defaultRegistry, log);
        return imageReplacer.UpdateImages(deploymentConfig.ImageReferences);
    }

    /// <summary>
    /// Processes helm values files using annotation-based image replacement paths.
    /// Converts annotation templates into HelmReference entries, then uses the same
    /// replacement logic as the step-variable path.
    /// </summary>
    protected FileUpdateResult ProcessHelmUpdateTargets(
        string workingDirectory,
        IReadOnlyCollection<HelmValuesFileImageUpdateTarget> targets)
    {
        var converter = new HelmAnnotationToReferenceConverter(defaultRegistry, log);
        var updatedImages = new HashSet<string>();
        var jsonPatches = new List<FileJsonPatch>();

        foreach (var target in targets)
        {
            var filepath = Path.Combine(workingDirectory, target.Path, target.FileName);
            var relativePath = Path.Combine(target.Path, target.FileName);

            var fileContent = fileSystem.ReadFile(filepath);
            var resolvedReferences = converter.Resolve(fileContent, target.ImagePathDefinitions, deploymentConfig.ImageReferences);
            if (resolvedReferences.Count == 0)
                continue;

            ProcessHelmValuesFile(filepath, relativePath, fileContent, resolvedReferences, updatedImages, jsonPatches);
        }

        return new FileUpdateResult(updatedImages, [], jsonPatches, []);
    }

    /// <summary>
    /// Processes helm values files using step-variable-based image replacement paths.
    /// </summary>
    protected FileUpdateResult ProcessHelmValuesFiles(HashSet<string> filesToUpdate,
                                                      string workingDirectory)
    {
        log.Verbose($"Found {filesToUpdate.Count} yaml files to process");

        var updatedImages = new HashSet<string>();
        var jsonPatches = new List<FileJsonPatch>();

        foreach (var file in filesToUpdate)
        {
            var relativePath = Path.GetRelativePath(workingDirectory, file);
            var fileContent = fileSystem.ReadFile(file);

            ProcessHelmValuesFile(file, relativePath, fileContent, deploymentConfig.ImageReferences, updatedImages, jsonPatches);
        }

        return new FileUpdateResult(updatedImages, [], jsonPatches, []);
    }

    void ProcessHelmValuesFile(
        string filepath,
        string relativePath,
        string fileContent,
        IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imageReferences,
        HashSet<string> updatedImages,
        List<FileJsonPatch> jsonPatches)
    {
        log.Info($"Processing file at {filepath}.");

        var replacer = new HelmContainerImageReplacer(fileContent, defaultRegistry, log);
        var result = replacer.UpdateImages(imageReferences);

        if (result.UpdatedImageReferences.Count > 0)
        {
            fileSystem.OverwriteFile(filepath, result.UpdatedContents);
            updatedImages.UnionWith(result.UpdatedImageReferences);
        }

        var patch = CreateJsonPatchWithPlaceholders(fileContent, imageReferences);
        if (patch != null)
            jsonPatches.Add(new FileJsonPatch(relativePath, JsonSerializer.Serialize(patch)));
    }

    /// <summary>
    /// Creates a JSON patch by running the replacer with placeholder tags to produce a "before",
    /// then running with real tags against the "before" to produce an "after", and diffing the two.
    /// </summary>
    JsonPatchDocument? CreateJsonPatchWithPlaceholders(
        string content,
        IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imageReferences)
    {
        var placeholderImages = imageReferences
            .Select(ir =>
            {
                var placeholderRef = MakePlaceholderRef(ir.ContainerReference.FriendlyName());
                return ir with { ContainerReference = ContainerImageReference.FromReferenceString(placeholderRef, defaultRegistry) };
            })
            .ToList();

        var placeholderResult = new HelmContainerImageReplacer(content, defaultRegistry, log).UpdateImages(placeholderImages);
        if (placeholderResult.UpdatedImageReferences.Count == 0)
            return null;

        var temporaryBefore = placeholderResult.UpdatedContents;
        var actualResult = new HelmContainerImageReplacer(temporaryBefore, defaultRegistry, log).UpdateImages(imageReferences);

        return actualResult.UpdatedImageReferences.Count > 0
            ? CreateJsonPatchFromDiff(temporaryBefore, actualResult.UpdatedContents)
            : null;
    }
}

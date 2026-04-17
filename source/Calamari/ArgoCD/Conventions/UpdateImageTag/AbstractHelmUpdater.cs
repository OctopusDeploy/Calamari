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

    protected override string CreateTemporaryBeforeContent(string content, HashSet<string> targetedImages)
    {
        // Bare tags (no colon) come from the step-variable path where only the tag
        // portion is tracked. A naive string replace could match unrelated values,
        // so we run the replacer with placeholder tags to target the correct YAML paths.
        var hasBareTags = targetedImages.Any(t => t.LastIndexOf(':') < 0);
        if (!hasBareTags)
        {
            return base.CreateTemporaryBeforeContent(content, targetedImages);
        }

        var placeholderImages = deploymentConfig.ImageReferences
            .Where(reference => reference.HelmReference is not null)
            .Select(reference =>
            {
                var friendlyName = reference.ContainerReference.FriendlyName();
                var colonIdx = friendlyName.LastIndexOf(':');
                var placeholderRef = colonIdx >= 0
                    ? friendlyName[..colonIdx] + ":__CALAMARI_PLACEHOLDER__"
                    : friendlyName + ":__CALAMARI_PLACEHOLDER__";
                return reference with
                {
                    ContainerReference = ContainerImageReference.FromReferenceString(placeholderRef, defaultRegistry)
                };
            })
            .ToList();

        var replacer = new HelmValuesImageReplaceStepVariables(content, defaultRegistry, log);
        var result = replacer.UpdateImages(placeholderImages);
        return result.UpdatedContents;
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

        var jsonPatch = CreateJsonPatch(fileContent,
                                        imagesToUpdate.Select(i => i.ContainerReference.FriendlyName()).ToHashSet(),
                                        tmp => new HelmContainerImageReplacer(tmp, target.DefaultClusterRegistry, target.ImagePathDefinitions, log).UpdateImages(imagesToUpdate));

        return new HelmRefUpdatedResult(imageUpdateResult.UpdatedImageReferences, Path.Combine(target.Path, target.FileName), jsonPatch);
    }
}

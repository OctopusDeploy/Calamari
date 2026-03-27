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
                   .Where(r => r.Updated)
                   .ToList();

        if (results.Any())
        {
            var patchedFiles = results
                .Select(r => new FileJsonPatch(r.RelativeFilepath, JsonSerializer.Serialize(r.JsonPatch)))
                .ToList();
            var updatedImages = results.SelectMany(r => r.ImagesUpdated).ToHashSet();

            return new FileUpdateResult(updatedImages, [], patchedFiles, []);
        }

        return new FileUpdateResult([], [], [], []);
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

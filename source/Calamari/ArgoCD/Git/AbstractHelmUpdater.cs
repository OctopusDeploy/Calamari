using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

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

    //NOTE: this is common with Helm Sources
    protected FileUpdateResult ProcessHelmValuesFiles(HashSet<string> filesToUpdate,
                                                      string workingDirectory,
                                                      ApplicationSourceWithMetadata sourceWithMetadata)
    {
        Func<string, IContainerImageReplacer> imageReplacerFactory = yaml => new HelmValuesImageReplaceStepVariables(yaml, defaultRegistry, log);
        log.Verbose($"Found {filesToUpdate.Count} yaml files to process");

        return Update(workingDirectory, deploymentConfig.ImageReferences, filesToUpdate.ToHashSet(), imageReplacerFactory);
    }

    /// <returns>Images that were updated</returns>
    protected FileUpdateResult ProcessHelmUpdateTargets(
        string workingDirectory,
        ApplicationSourceWithMetadata sourceWithMetadata,
        IReadOnlyCollection<HelmValuesFileImageUpdateTarget> targets)
    {
        var results = targets.Select(t => UpdateHelmImageValues(workingDirectory,
                                                                t,
                                                                deploymentConfig.ImageReferences
                                                               ))
                             .Where(r => r.ImagesUpdated.Any())
                             .ToList();

        if (results.Any())
        {
            var patchedFiles = results.Select(r => new FilePathContent(
                                                                       // Replace \ with / so that Calamari running on windows doesn't cause issues when we send back to server
                                                                       r.RelativeFilepath.Replace('\\', '/'),
                                                                       JsonSerializer.Serialize(r.JsonPatch)))
                                      .ToList();
            var updatedImages = results.SelectMany(r => r.ImagesUpdated).ToHashSet();

            return new FileUpdateResult(patchedFiles.Select(pf => pf.FilePath).ToHashSet(), updatedImages, patchedFiles);
        }

        return new FileUpdateResult([], [], []);
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
        {
            fileSystem.OverwriteFile(filepath, imageUpdateResult.UpdatedContents);
            var jsonPatch = UpdaterHelpers.CreateJsonPatch(fileContent, imageUpdateResult.UpdatedContents);
            return new HelmRefUpdatedResult(imageUpdateResult.UpdatedImageReferences, Path.Combine(target.Path, target.FileName), jsonPatch);
        }

        return new HelmRefUpdatedResult(new HashSet<string>(), Path.Combine(target.Path, target.FileName), null);
    }
}
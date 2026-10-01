using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class DirectoryUpdater: BaseUpdater
{
    readonly IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate;
    readonly string defaultRegistry;

    public DirectoryUpdater(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
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
        var replacer = new ContainerImageReplacer(input, defaultRegistry);
        return replacer.UpdateImages(imagesToUpdate);
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

        return UpdateKubernetesYaml(workingDirectory, applicationSource.Path!);
    }

    FileUpdateResult UpdateKubernetesYaml(
        string rootPath,
        string subFolder)
    {
        var absSubFolder = Path.Combine(rootPath, subFolder);
        var filesToUpdate = FindYamlFiles(absSubFolder).ToHashSet();
        log.Verbose($"Found {filesToUpdate.Count} yaml files to process");

        return Update(rootPath, filesToUpdate);
    }

    //NOTE: rootPath needs to include the subfolder
    IEnumerable<string> FindYamlFiles(string rootPath)
    {
        var yamlFileGlob = "**/*.{yaml,yml}";
        return fileSystem.EnumerateFilesWithGlob(rootPath, yamlFileGlob);
    }
}
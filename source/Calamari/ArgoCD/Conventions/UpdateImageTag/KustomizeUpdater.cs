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
            return new FileUpdateResult( [], [], []);
        }
        
        log.Verbose($"Reading files from {applicationSource.Path}");
        return UpdateKustomizeYaml(workingDirectory, applicationSource.Path!);
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
        return new FileUpdateResult( [], [], []);
    }
}
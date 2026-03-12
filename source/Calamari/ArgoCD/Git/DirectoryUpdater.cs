using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class DirectoryUpdater: BaseUpdater
{
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly string defaultRegistry;

    public DirectoryUpdater(UpdateArgoCDAppDeploymentConfig deploymentConfig,
                            string defaultRegistry,
                            ILog log,
                            ICalamariFileSystem fileSystem) : base(log,
                                                                   fileSystem)
    {
        this.deploymentConfig = deploymentConfig;
        this.defaultRegistry = defaultRegistry;
    }

    public override FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        var applicationSource = sourceWithMetadata.Source;
        if (applicationSource.Path == null)
        {
            log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
            return new FileUpdateResult(new HashSet<string>(), [], []);
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
        Func<string, IContainerImageReplacer> imageReplacerFactory = yaml => new ContainerImageReplacer(yaml, defaultRegistry);
        log.Verbose($"Found {filesToUpdate.Count} yaml files to process");

        return Update(rootPath, deploymentConfig.ImageReferences, filesToUpdate, imageReplacerFactory);
    }

    //NOTE: rootPath needs to include the subfolder
    IEnumerable<string> FindYamlFiles(string rootPath)
    {
        var yamlFileGlob = "**/*.{yaml,yml}";
        return fileSystem.EnumerateFilesWithGlob(rootPath, yamlFileGlob);
    }
}
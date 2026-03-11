using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class DirectoryUpdater : BaseUpdater
{
    readonly Application applicationFromYaml;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly string defaultRegistry;
    readonly ArgoCDGatewayDto gateway;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

    public DirectoryUpdater(Application applicationFromYaml,
                            Dictionary<string, GitCredentialDto> gitCredentials,
                            RepositoryFactory repositoryFactory,
                            UpdateArgoCDAppDeploymentConfig deploymentConfig,
                            string defaultRegistry,
                            ArgoCDGatewayDto gateway,
                            ILog log,
                            ICommitMessageGenerator commitMessageGenerator,
                            ICalamariFileSystem fileSystem,
                            ArgoCDOutputVariablesWriter outputVariablesWriter) : base(repositoryFactory,
                                                                                      gitCredentials,
                                                                                      log,
                                                                                      commitMessageGenerator,
                                                                                      fileSystem)
    {
        this.applicationFromYaml = applicationFromYaml;
        this.gitCredentials = gitCredentials;
        this.deploymentConfig = deploymentConfig;
        this.defaultRegistry = defaultRegistry;
        this.gateway = gateway;
        this.outputVariablesWriter = outputVariablesWriter;
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

        return UpdateKubernetesYaml(workingDirectory, applicationSource.Path!, defaultRegistry, deploymentConfig.ImageReferences);
    }

    FileUpdateResult UpdateKubernetesYaml(
        string rootPath,
        string subFolder,
        string defaultRegistry,
        IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var absSubFolder = Path.Combine(rootPath, subFolder);

        var filesToUpdate = FindYamlFiles(absSubFolder).ToHashSet();
        Func<string, IContainerImageReplacer> imageReplacerFactory = yaml => new ContainerImageReplacer(yaml, defaultRegistry);
        log.Verbose($"Found {filesToUpdate.Count} yaml files to process");

        return Update(rootPath, imagesToUpdate, filesToUpdate, imageReplacerFactory);
    }

    //NOTE: rootPath needs to include the subfolder
    IEnumerable<string> FindYamlFiles(string rootPath)
    {
        var yamlFileGlob = "**/*.{yaml,yml}";
        return fileSystem.EnumerateFilesWithGlob(rootPath, yamlFileGlob);
    }
}
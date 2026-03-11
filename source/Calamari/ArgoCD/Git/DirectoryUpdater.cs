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
    readonly ICalamariFileSystem fileSystem;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

    public DirectoryUpdater(Application applicationFromYaml, Dictionary<string, GitCredentialDto> gitCredentials, RepositoryFactory repositoryFactory, UpdateArgoCDAppDeploymentConfig deploymentConfig,
                            string defaultRegistry,
                            ArgoCDGatewayDto gateway,
                            ILog log,
                            ICommitMessageGenerator commitMessageGenerator,
                            ICalamariFileSystem fileSystem,
                            ArgoCDOutputVariablesWriter outputVariablesWriter) : base(repositoryFactory, gitCredentials, log, commitMessageGenerator)
    {
        this.applicationFromYaml = applicationFromYaml;
        this.gitCredentials = gitCredentials;
        this.deploymentConfig = deploymentConfig;
        this.defaultRegistry = defaultRegistry;
        this.gateway = gateway;
        this.fileSystem = fileSystem;
        this.outputVariablesWriter = outputVariablesWriter;
    }

    public SourceUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata)
        {
            var applicationSource = sourceWithMetadata.Source;
            if (applicationSource.Path == null)
            {
                log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
                return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
            }

            using (var repository = CreateRepository(sourceWithMetadata))
            {
                log.Verbose($"Reading files from {applicationSource.Path}");

                var (updatedFiles, updatedImages, patchedFiles) = UpdateKubernetesYaml(repository.WorkingDirectory, applicationSource.Path!, defaultRegistry, deploymentConfig.ImageReferences);
                if (updatedImages.Count > 0)
                {
                    var pushResult = PushToRemote(repository,
                                                  GitReference.CreateFromString(applicationSource.TargetRevision),
                                                  deploymentConfig.CommitParameters,
                                                  updatedFiles,
                                                  updatedImages);

                    if (pushResult is not null)
                    {
                        outputVariablesWriter.WritePushResultOutput(gateway.Name,
                                                                    applicationFromYaml.Metadata.Name,
                                                                    sourceWithMetadata.Index,
                                                                    pushResult);
                        return new SourceUpdateResult(updatedImages, pushResult.CommitSha, patchedFiles);
                    }

                    return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
                }
            }

            return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
        }
    
    (HashSet<string>, HashSet<string>, List<FilePathContent>) UpdateKubernetesYaml(
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
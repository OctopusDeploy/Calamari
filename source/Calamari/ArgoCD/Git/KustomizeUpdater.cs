using System;
using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class KustomizeUpdater : BaseUpdater
{
    readonly Application applicationFromYaml;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly string defaultRegistry;
    readonly ArgoCDGatewayDto gateway;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

    public KustomizeUpdater(Application applicationFromYaml, Dictionary<string, GitCredentialDto> gitCredentials, RepositoryFactory repositoryFactory, UpdateArgoCDAppDeploymentConfig deploymentConfig,
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
        this.deploymentConfig = deploymentConfig;
        this.defaultRegistry = defaultRegistry;
        this.gateway = gateway;
        this.outputVariablesWriter = outputVariablesWriter;
    }

    public override SourceUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata)
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

            var (updatedFiles, updatedImages, patchedFiles) = UpdateKustomizeYaml(repository.WorkingDirectory, applicationSource.Path!, defaultRegistry, deploymentConfig.ImageReferences);
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
            }
        }

        return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
    }
    
    (HashSet<string>, HashSet<string>, List<FilePathContent>) UpdateKustomizeYaml(
        string rootPath,
        string subFolder,
        string defaultRegistry,
        IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var absSubFolder = Path.Combine(rootPath, subFolder);

        Func<string, IContainerImageReplacer> imageReplacerFactory;
        HashSet<string> filesToUpdate;

        var kustomizationFile = KustomizeDiscovery.TryFindKustomizationFile(fileSystem, absSubFolder);
        if (kustomizationFile != null)
        {
            filesToUpdate = new HashSet<string> { kustomizationFile };
            imageReplacerFactory = yaml => new KustomizeImageReplacer(yaml, defaultRegistry, log);
            log.Verbose("kustomization file found, will only update images transformer in the kustomization file");
            return Update(rootPath, imagesToUpdate, filesToUpdate, imageReplacerFactory);
        }

        log.Warn("kustomization file not found, no files will be updated");
        return ([], [], []);
    }
}
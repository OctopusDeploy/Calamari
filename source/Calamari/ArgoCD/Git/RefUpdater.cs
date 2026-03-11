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

public class RefUpdater : BaseUpdater
{
    readonly Application applicationFromYaml;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly string defaultRegistry;
    readonly ArgoCDGatewayDto gateway;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

    public RefUpdater(Application applicationFromYaml,
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
        this.deploymentConfig = deploymentConfig;
    }

    public override SourceUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        var applicationSource = sourceWithMetadata.Source;

        if (applicationSource.Path != null)
        {
            log.WarnFormat("The source '{0}' contains a Ref, only referenced files will be updated. Please create another source with the same URL if you wish to update files under the path.", sourceWithMetadata.SourceIdentity);
        }

        using var repository = CreateRepository(sourceWithMetadata);
        if (deploymentConfig.HasStepBasedHelmValueReferences())
        {
            if (applicationFromYaml.Metadata.Annotations.ContainsKey(ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(new ApplicationSourceName(sourceWithMetadata.Source.Name))))
            {
                log.Warn($"Application {applicationFromYaml.Metadata.Name} specifies helm-value annotations which have been superseded by container-values specified in the step's configuration");
            }

            return ProcessRefSourceUsingStepVariables(applicationFromYaml,
                                                      sourceWithMetadata,
                                                      repository,
                                                      deploymentConfig,
                                                      defaultRegistry,
                                                      gateway);
        }

        var helmTargetsForRefSource = new HelmValuesFileUpdateTargetParser(applicationFromYaml, defaultRegistry)
            .GetHelmTargetsForRefSource(sourceWithMetadata);

        HelmHelpers.LogHelmSourceConfigurationProblems(helmTargetsForRefSource.Problems);

        return ProcessHelmUpdateTargets(
                                        applicationFromYaml,
                                        repository,
                                        deploymentConfig,
                                        sourceWithMetadata,
                                        helmTargetsForRefSource.Targets,
                                        gateway);
    }

    SourceUpdateResult ProcessRefSourceUsingStepVariables(Application applicationFromYaml,
                                                          ApplicationSourceWithMetadata sourceWithMetadata,
                                                          RepositoryWrapper repository,
                                                          UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                                          string defaultRegistry,
                                                          ArgoCDGatewayDto gateway)
    {
        var extractor = new HelmValuesFileExtractor(applicationFromYaml);
        var valuesFiles = extractor.GetValueFilesReferencedInRefSource(sourceWithMetadata)
                                   .Select(file => Path.Combine(repository.WorkingDirectory, file));
        return ProcessHelmValuesFiles(valuesFiles.ToHashSet(),
                                      repository,
                                      sourceWithMetadata);
    }

    //NOTE: this is common with Helm Sources
    SourceUpdateResult ProcessHelmValuesFiles(HashSet<string> filesToUpdate,
                                              RepositoryWrapper repository,
                                              ApplicationSourceWithMetadata sourceWithMetadata)
    {
        Func<string, IContainerImageReplacer> imageReplacerFactory = yaml => new HelmValuesImageReplaceStepVariables(yaml, defaultRegistry, log);
        log.Verbose($"Found {filesToUpdate.Count} yaml files to process");

        var (updatedFiles, updatedImages, patchedFiles) = Update(repository.WorkingDirectory, deploymentConfig.ImageReferences, filesToUpdate.ToHashSet(), imageReplacerFactory);
        if (updatedImages.Count > 0)
        {
            Log.Info("Trying to push up changes");
            var pushResult = PushToRemote(repository,
                                          GitReference.CreateFromString(sourceWithMetadata.Source.TargetRevision),
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

        return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
    }
    
    //TODO(tmm): This is common with stanard helm too.
    /// <returns>Images that were updated</returns>
        SourceUpdateResult ProcessHelmUpdateTargets(
            RepositoryWrapper repository,
            ApplicationSourceWithMetadata sourceWithMetadata,
            IReadOnlyCollection<HelmValuesFileImageUpdateTarget> targets)
        {
            var results = targets.Select(t => UpdateHelmImageValues(repository.WorkingDirectory,
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

                var pushResult = PushToRemote(repository,
                                              GitReference.CreateFromString(sourceWithMetadata.Source.TargetRevision),
                                              deploymentConfig.CommitParameters,
                                              results.Select(r => r.RelativeFilepath).ToHashSet(),
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

            return new SourceUpdateResult([], string.Empty, []);
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
            var jsonPatch = CreateJsonPatch(fileContent, imageUpdateResult.UpdatedContents);
            return new HelmRefUpdatedResult(imageUpdateResult.UpdatedImageReferences, Path.Combine(target.Path, target.FileName), jsonPatch);
        }

        return new HelmRefUpdatedResult(new HashSet<string>(), Path.Combine(target.Path, target.FileName), null);
    }
}
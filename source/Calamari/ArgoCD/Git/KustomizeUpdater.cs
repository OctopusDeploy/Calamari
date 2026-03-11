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
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Git;

public class KustomizeUpdater : BaseUpdater
{
    readonly Application applicationFromYaml;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly string defaultRegistry;
    readonly ArgoCDGatewayDto gateway;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

    public KustomizeUpdater(RepositoryFactory repositoryFactory,
                            Dictionary<string, GitCredentialDto> gitCredentials,
                            ILog log,
                            ICommitMessageGenerator commitMessageGenerator,
                            ICalamariFileSystem fileSystem,
                            Application applicationFromYaml,
                            UpdateArgoCDAppDeploymentConfig deploymentConfig,
                            string defaultRegistry,
                            ArgoCDGatewayDto gateway,
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

        HelmHelpers.LogHelmSourceConfigurationProblems(log,helmTargetsForRefSource.Problems);

        return ProcessHelmUpdateTargets(repository,
                                        sourceWithMetadata,
                                        helmTargetsForRefSource.Targets);
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
                                      defaultRegistry,
                                      repository,
                                      deploymentConfig,
                                      gateway,
                                      sourceWithMetadata,
                                      applicationFromYaml);
    }
    
           /// <returns>Images that were updated</returns>
        UpdateArgoCDAppImagesInstallConvention.SourceUpdateResult ProcessHelmUpdateTargets(
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
                    return new UpdateArgoCDAppImagesInstallConvention.SourceUpdateResult(updatedImages, pushResult.CommitSha, patchedFiles);
                }
            }

            return new UpdateArgoCDAppImagesInstallConvention.SourceUpdateResult([], string.Empty, []);
        }
}
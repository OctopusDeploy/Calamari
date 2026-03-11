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

public class RefUpdater : AbstractHelmUpdater
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
                                                                                fileSystem,
                                                                                deploymentConfig,
                                                                                defaultRegistry,
                                                                                gateway,
                                                                                outputVariablesWriter,
                                                                                applicationFromYaml)
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

            return ProcessRefSourceUsingStepVariables(sourceWithMetadata,
                                                      repository);
        }

        var helmTargetsForRefSource = new HelmValuesFileUpdateTargetParser(applicationFromYaml, defaultRegistry)
            .GetHelmTargetsForRefSource(sourceWithMetadata);

        HelmHelpers.LogHelmSourceConfigurationProblems(log, helmTargetsForRefSource.Problems);

        return ProcessHelmUpdateTargets(repository,
                                        sourceWithMetadata,
                                        helmTargetsForRefSource.Targets);
    }

    SourceUpdateResult ProcessRefSourceUsingStepVariables(ApplicationSourceWithMetadata sourceWithMetadata,
                                                          RepositoryWrapper repository)
    {
        var extractor = new HelmValuesFileExtractor(applicationFromYaml);
        var valuesFiles = extractor.GetValueFilesReferencedInRefSource(sourceWithMetadata)
                                   .Select(file => Path.Combine(repository.WorkingDirectory, file));
        return ProcessHelmValuesFiles(valuesFiles.ToHashSet(),
                                      repository,
                                      sourceWithMetadata);
    }
}
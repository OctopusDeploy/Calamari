using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class ApplicationSourceUpdater
{
    readonly Application applicationFromYaml;
    readonly ArgoCDGatewayDto gateway;
    readonly RepositoryAdapter repositoryAdapter;
    readonly DeploymentScope deploymentScope;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly ILog log;
    readonly string defaultRegistry;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;
    readonly ICalamariFileSystem fileSystem;

    public ApplicationSourceUpdater(Application applicationFromYaml,
                                    RepositoryAdapter repositoryAdapter,
                                    DeploymentScope deploymentScope,
                                    UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                    ILog log,
                                    ArgoCDGatewayDto gateway,
                                    string defaultRegistry,
                                    ArgoCDOutputVariablesWriter outputVariablesWriter,
                                    ICalamariFileSystem fileSystem)
    {
        this.applicationFromYaml = applicationFromYaml;
        this.repositoryAdapter = repositoryAdapter;
        this.deploymentScope = deploymentScope;
        this.deploymentConfig = deploymentConfig;
        this.log = log;
        this.gateway = gateway;
        this.defaultRegistry = defaultRegistry;
        this.outputVariablesWriter = outputVariablesWriter;
        this.fileSystem = fileSystem;
    }

    public SourceUpdateResult ProcessSource(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;

        var applicationSource = sourceWithMetadata.Source;
        var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources);

        log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);
        if (!deploymentScope.Matches(annotatedScope))
        {
            return new SourceUpdateResult(new HashSet<string>(), null, [], []);
        }
        
        var sourceUpdater = CreateSpecificUpdater(sourceWithMetadata);

        var sourceUpdateResult = repositoryAdapter.Process(sourceWithMetadata, sourceUpdater);

        if (sourceUpdateResult.PushResult is not null)
        {
            outputVariablesWriter.WritePushResultOutput(gateway.Name,
                                                        applicationFromYaml.Metadata.Name,
                                                        sourceWithMetadata.Index,
                                                        sourceUpdateResult.PushResult);
        }

        return sourceUpdateResult;
    }

    ISourceUpdater CreateSpecificUpdater(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        ISourceUpdater sourceUpdater;
        if (sourceWithMetadata.SourceType == SourceType.Directory)
        {
            if (sourceWithMetadata.Source.Ref == null)
            {
                sourceUpdater = new DirectoryUpdater(deploymentConfig.ImageReferences,
                                                     defaultRegistry,
                                                     log,
                                                     fileSystem);
            }
            else
            {
                sourceUpdater = new RefUpdater(applicationFromYaml,
                                               deploymentConfig,
                                               defaultRegistry,
                                               log,
                                               fileSystem);
            }
        }
        else if (sourceWithMetadata.SourceType == SourceType.Helm)
        {
            sourceUpdater = new HelmUpdater(applicationFromYaml,
                                            deploymentConfig,
                                            defaultRegistry,
                                            log,
                                            fileSystem);
        }
        else if (sourceWithMetadata.SourceType == SourceType.Kustomize)
        {
            sourceUpdater = new KustomizeUpdater(deploymentConfig.ImageReferences,
                                                 defaultRegistry,
                                                 log,
                                                 fileSystem);
        }
        else if (sourceWithMetadata.SourceType == SourceType.Plugin)
        {
            log.WarnFormat("Unable to update source '{0}' as Plugin sources aren't currently supported.", sourceWithMetadata.SourceIdentity);
            sourceUpdater = new NoOpSourceUpdater();
        }
        else
        {
            throw new ArgumentOutOfRangeException();
        }

        return sourceUpdater;
    }
}
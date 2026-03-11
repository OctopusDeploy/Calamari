using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class ApplicationSourceUpdater
{
    Application applicationFromYaml;
    ArgoCDGatewayDto gateway;
    RepositoryFactory repositoryFactory;
    ArgoCDCustomPropertiesDto argoCDCustomPropertiesDto;
    Dictionary<string, GitCredentialDto> gitCredentials;
    DeploymentScope deploymentScope;
    UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly ILog log;
    string defaultRegistry;
    readonly ICommitMessageGenerator commitMessageGenerator;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;
    readonly ICalamariFileSystem fileSystem;

    public ApplicationSourceUpdater(Application applicationFromYaml,
                                    RepositoryFactory repositoryFactory,
                                    ArgoCDCustomPropertiesDto argoCDCustomPropertiesDto,
                                    Dictionary<string, GitCredentialDto> gitCredentials,
                                    DeploymentScope deploymentScope,
                                    UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                    ILog log,
                                    ArgoCDGatewayDto gateway,
                                    string defaultRegistry,
                                    ICommitMessageGenerator commitMessageGenerator,
                                    ArgoCDOutputVariablesWriter outputVariablesWriter,
                                    ICalamariFileSystem fileSystem)
    {
        this.applicationFromYaml = applicationFromYaml;
        this.repositoryFactory = repositoryFactory;
        this.argoCDCustomPropertiesDto = argoCDCustomPropertiesDto;
        this.gitCredentials = gitCredentials;
        this.deploymentScope = deploymentScope;
        this.deploymentConfig = deploymentConfig;
        this.log = log;
        this.gateway = gateway;
        this.defaultRegistry = defaultRegistry;
        this.commitMessageGenerator = commitMessageGenerator;
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
            return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
        }

        ISourceUpdater sourceUpdater;
        if (sourceWithMetadata.SourceType == SourceType.Directory)
        {
            if (applicationSource.Ref == null)
            {
                sourceUpdater = new DirectoryUpdater(applicationFromYaml,
                                                     gitCredentials,
                                                     repositoryFactory,
                                                     deploymentConfig,
                                                     defaultRegistry,
                                                     gateway,
                                                     log,
                                                     commitMessageGenerator,
                                                     fileSystem,
                                                     outputVariablesWriter);
            }
            else
            {
                sourceUpdater = new RefUpdater(applicationFromYaml,
                                               gitCredentials,
                                               repositoryFactory,
                                               deploymentConfig,
                                               defaultRegistry,
                                               gateway,
                                               log,
                                               commitMessageGenerator,
                                               fileSystem,
                                               outputVariablesWriter);
                
            }
        }
        else if (sourceWithMetadata.SourceType == SourceType.Helm)
        {
            sourceUpdater = new HelmUpdater(applicationFromYaml,
                                          gitCredentials,
                                          repositoryFactory,
                                          deploymentConfig,
                                          defaultRegistry,
                                          gateway,
                                          log,
                                          commitMessageGenerator,
                                          fileSystem,
                                          outputVariablesWriter);
        }
        else if (sourceWithMetadata.SourceType == SourceType.Kustomize)
        {
            sourceUpdater = new KustomizeUpdater(applicationFromYaml,
                                               gitCredentials,
                                               repositoryFactory,
                                               deploymentConfig,
                                               defaultRegistry,
                                               gateway,
                                               log,
                                               commitMessageGenerator,
                                               fileSystem,
                                               outputVariablesWriter);
        }
        else if (sourceWithMetadata.SourceType == SourceType.Plugin)
        {
            log.WarnFormat("Unable to update source '{0}' as Plugin sources aren't currently supported.", sourceWithMetadata.SourceIdentity);
            return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
        }
        else
        {
            throw new ArgumentOutOfRangeException();
        }

        var repoAdapter = new RepositoryAdapter(gitCredentials,
                                                repositoryFactory,
                                                deploymentConfig,
                                                log,
                                                commitMessageGenerator,
                                                sourceUpdater);
        
        var sourceUpdateResult = repoAdapter.Process(sourceWithMetadata);

        if (sourceUpdateResult.PushResult is not null)
        {
            outputVariablesWriter.WritePushResultOutput(gateway.Name,
                                                        applicationFromYaml.Metadata.Name,
                                                        sourceWithMetadata.Index,
                                                        sourceUpdateResult.PushResult);   
        }

        return sourceUpdateResult;
    }
}
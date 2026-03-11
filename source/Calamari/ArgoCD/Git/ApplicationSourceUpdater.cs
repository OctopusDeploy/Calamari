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

    public ApplicationSourceUpdater(Application applicationFromYaml, RepositoryFactory repositoryFactory, ArgoCDCustomPropertiesDto argoCDCustomPropertiesDto, Dictionary<string, GitCredentialDto> gitCredentials,
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

            if (sourceWithMetadata.SourceType == SourceType.Directory)
            {
                var updater = new DirectoryUpdater(applicationFromYaml,
                                                   gitCredentials,
                                                   repositoryFactory,
                                                   deploymentConfig,
                                                   defaultRegistry,
                                                   gateway,
                                                   log,
                                                   commitMessageGenerator,
                                                   fileSystem,
                                                   outputVariablesWriter);
                updater.Process(sourceWithMetadata);
            }
            else if (sourceWithMetadata.SourceType == SourceType.Helm)
            {
                
            }
            else if (sourceWithMetadata.SourceType == SourceType.Kustomize)
            {
                
            }

            updater.Process(sourceWithMetadata);
            
            switch (sourceWithMetadata.SourceType)
            {
                case SourceType.Directory:
                {
                    return applicationSource.Ref != null
                        ? ProcessRef(applicationFromYaml,
                                     gitCredentials,
                                     repositoryFactory,
                                     deploymentConfig,
                                     sourceWithMetadata,
                                     defaultRegistry,
                                     gateway)
                        : 
                }
                case SourceType.Helm:
                {
                    return ProcessHelm(applicationFromYaml,
                                       gitCredentials,
                                       repositoryFactory,
                                       deploymentConfig,
                                       sourceWithMetadata,
                                       defaultRegistry,
                                       gateway);
                }
                case SourceType.Kustomize:
                {
                    return ProcessKustomize(applicationFromYaml,
                                            gitCredentials,
                                            repositoryFactory,
                                            deploymentConfig,
                                            sourceWithMetadata,
                                            defaultRegistry,
                                            gateway);
                }
                case SourceType.Plugin:
                {
                    log.WarnFormat("Unable to update source '{0}' as Plugin sources aren't currently supported.", sourceWithMetadata.SourceIdentity);
                    return new SourceUpdateResult(new HashSet<string>(), string.Empty, []);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
    }
}
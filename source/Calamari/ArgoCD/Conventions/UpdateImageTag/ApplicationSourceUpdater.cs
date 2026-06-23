using System;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

// Determines whether a source is in scope for this deployment and builds the appropriate file updater for it.
// The actual clone/commit/push is handled centrally by the GroupedRepositoryProcessor.
public class ApplicationSourceUpdater
{
    readonly Application applicationFromYaml;
    readonly DeploymentScope deploymentScope;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly ILog log;
    readonly string defaultRegistry;
    readonly ICalamariFileSystem fileSystem;

    public ApplicationSourceUpdater(Application applicationFromYaml,
                                    DeploymentScope deploymentScope,
                                    UpdateArgoCDAppDeploymentConfig deploymentConfig,
                                    ILog log,
                                    string defaultRegistry,
                                    ICalamariFileSystem fileSystem)
    {
        this.applicationFromYaml = applicationFromYaml;
        this.deploymentScope = deploymentScope;
        this.deploymentConfig = deploymentConfig;
        this.log = log;
        this.defaultRegistry = defaultRegistry;
        this.fileSystem = fileSystem;
    }

    public bool IsAppInScope(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;

        var applicationSource = sourceWithMetadata.Source;
        var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources);

        log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);
        return deploymentScope.Matches(annotatedScope);
    }

    public ISourceUpdater CreateSourceUpdater(ApplicationSourceWithMetadata sourceWithMetadata)
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
            sourceUpdater = new KustomizeUpdater(deploymentConfig,
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

using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Conventions.ManifestTemplating;

// Determines whether a source is in scope for this deployment and builds the file updater (which copies the
// templated package files into the source path). Clone/commit/push is handled by the GroupedRepositoryProcessor.
public class ApplicationSourceFactory
{
    readonly Application applicationFromYaml;
    readonly DeploymentScope deploymentScope;
    readonly ArgoCommitToGitConfig deploymentConfig;
    readonly IPackageRelativeFile[] packageFiles;
    readonly ILog log;
    readonly ICalamariFileSystem fileSystem;

    public ApplicationSourceFactory(Application applicationFromYaml,
                                    DeploymentScope deploymentScope,
                                    ArgoCommitToGitConfig deploymentConfig,
                                    IPackageRelativeFile[] packageFiles,
                                    ILog log,
                                    ICalamariFileSystem fileSystem)
    {
        this.applicationFromYaml = applicationFromYaml;
        this.deploymentScope = deploymentScope;
        this.deploymentConfig = deploymentConfig;
        this.packageFiles = packageFiles;
        this.log = log;
        this.fileSystem = fileSystem;
    }

    public bool IsAppInScope(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        var applicationSource = sourceWithMetadata.Source;
        var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, applicationFromYaml.Spec.Sources.Count > 1);

        log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);

        return deploymentScope.Matches(annotatedScope);
    }

    public ISourceUpdater CreateSourceUpdater(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        var applicationSource = sourceWithMetadata.Source;
        applicationFromYaml.Metadata.Annotations.TryGetValue(ArgoCDConstants.Annotations.OctopusPathAnnotationKey(applicationSource.Name.ToApplicationSourceName()), out var pathOverrideFromAnnotation);

        return new CopyTemplatesSourceUpdater(packageFiles, log, fileSystem, deploymentConfig.PurgeOutputDirectory, pathOverrideFromAnnotation);
    }
}

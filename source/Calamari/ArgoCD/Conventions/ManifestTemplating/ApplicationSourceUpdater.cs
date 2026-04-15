using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.ManifestTemplating;

public class ApplicationSourceUpdater
{
    readonly Application applicationFromYaml;
    readonly DeploymentScope deploymentScope;
    readonly RepositoryAdapter repositoryAdapter;
    readonly ArgoCommitToGitConfig deploymentConfig;
    readonly IPackageRelativeFile[] packageFiles;
    readonly ArgoCDGatewayDto gateway;
    readonly ILog log;
    readonly ICalamariFileSystem fileSystem;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

    public ApplicationSourceUpdater(Application applicationFromYaml,
                                    ArgoCDGatewayDto gateway,
                                    DeploymentScope deploymentScope,
                                    ArgoCommitToGitConfig deploymentConfig,
                                    IPackageRelativeFile[] packageFiles,
                                    ILog log,
                                    ICalamariFileSystem fileSystem,
                                    ArgoCDOutputVariablesWriter outputVariablesWriter,
                                    RepositoryAdapter repositoryAdapter)
    {
        this.applicationFromYaml = applicationFromYaml;
        this.deploymentScope = deploymentScope;
        this.deploymentConfig = deploymentConfig;
        this.packageFiles = packageFiles;
        this.gateway = gateway;
        this.log = log;
        this.fileSystem = fileSystem;
        this.outputVariablesWriter = outputVariablesWriter;
        this.repositoryAdapter = repositoryAdapter;
    }

    public bool IsAppInScope(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        var applicationSource = sourceWithMetadata.Source;
        var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, applicationFromYaml.Spec.Sources.Count > 1);

        log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);

        return deploymentScope.Matches(annotatedScope);
    }

    public ManifestUpdateResult ProcessSource(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        var applicationSource = sourceWithMetadata.Source;
        log.Info($"Writing files to repository '{applicationSource.OriginalRepoUrl}' for '{applicationFromYaml.Metadata.Name}'");

        applicationFromYaml.Metadata.Annotations.TryGetValue(ArgoCDConstants.Annotations.OctopusPathAnnotationKey(applicationSource.Name.ToApplicationSourceName()), out var pathOverrideFromAnnotation); 
        
        var sourceUpdater = new CopyTemplatesSourceUpdater(packageFiles, log, fileSystem, deploymentConfig.PurgeOutputDirectory, pathOverrideFromAnnotation);

        var sourceUpdateResult = repositoryAdapter.Process(sourceWithMetadata.Source.OriginalRepoUrl, sourceWithMetadata.Source.TargetRevision,  workingDir => sourceUpdater.Process(sourceWithMetadata, workingDir));

        if (sourceUpdateResult.PushResult is not null)
        {
            outputVariablesWriter.WritePushResultOutput(gateway.Name,
                                                        applicationFromYaml.Metadata.Name,
                                                        sourceWithMetadata.Index,
                                                        sourceUpdateResult.PushResult);

            return new ManifestUpdateResult(true, sourceUpdateResult.PushResult.CommitSha, sourceUpdateResult.PushResult.CommitTimestamp, sourceUpdateResult.ReplacedFiles);
        }

        log.Info("No changes were committed");
        return new ManifestUpdateResult(false, null, null, sourceUpdateResult.ReplacedFiles);
    }
}

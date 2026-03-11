using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class HelmUpdater : BaseUpdater
{
    readonly Application applicationFromYaml;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly string defaultRegistry;
    readonly ArgoCDGatewayDto gateway;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;
    
    public HelmUpdater(Application applicationFromYaml,
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

        if (deploymentConfig.HasStepBasedHelmValueReferences())
        {
            var appName = sourceWithMetadata.Source.Name.IsNullOrEmpty() ? null : new ApplicationSourceName(sourceWithMetadata.Source.Name);
            if (applicationFromYaml.Metadata.Annotations.ContainsKey(ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(appName)))
            {
                log.Warn($"Application '{applicationFromYaml.Metadata.Name}' specifies helm-value annotations which have been superseded by values specified in the step's configuration");
            }

            return ProcessHelmSourceUsingStepVariables(applicationFromYaml,
                                                       gitCredentials,
                                                       repositoryFactory,
                                                       deploymentConfig,
                                                       sourceWithMetadata,
                                                       defaultRegistry,
                                                       gateway);
        }

        return ProcessHelmSourceUsingAnnotations(applicationFromYaml,
                                                 sourceWithMetadata,
                                                 gitCredentials,
                                                 repositoryFactory,
                                                 deploymentConfig,
                                                 defaultRegistry,
                                                 gateway,
                                                 applicationSource);
    }
    
    SourceUpdateResult ProcessHelmSourceUsingStepVariables(
        Application applicationFromYaml,
        Dictionary<string, GitCredentialDto> gitCredentials,
        RepositoryFactory repositoryFactory,
        UpdateArgoCDAppDeploymentConfig deploymentConfig,
        ApplicationSourceWithMetadata sourceWithMetadata,
        string defaultRegistry,
        ArgoCDGatewayDto gateway)
    {
        var extractor = new HelmValuesFileExtractor(applicationFromYaml);
        var valuesFilesInHelmSource = extractor.GetInlineValuesFilesReferencedByHelmSource(sourceWithMetadata);

        using var repository = CreateRepository(sourceWithMetadata);
        var filesToUpdate = valuesFilesInHelmSource.Select(file => Path.Combine(repository.WorkingDirectory, file)).ToList();
        var implicitValuesFile = HelmDiscovery.TryFindValuesFile(fileSystem, Path.Combine(repository.WorkingDirectory, sourceWithMetadata.Source.Path!));
        if (implicitValuesFile != null)
        {
            implicitValuesFile = Path.Combine(repository.WorkingDirectory, sourceWithMetadata.Source.Path!, implicitValuesFile);
            filesToUpdate.Add(implicitValuesFile);
        }

        filesToUpdate = filesToUpdate.Select(file => Path.Combine(repository.WorkingDirectory, file)).ToList();
        var result = ProcessHelmValuesFiles(filesToUpdate.ToHashSet(),
                                            defaultRegistry,
                                            repository,
                                            deploymentConfig,
                                            gateway,
                                            sourceWithMetadata,
                                            applicationFromYaml);
        return result;
    }
}
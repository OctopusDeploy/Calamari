using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class ApplicationUpdater
{
    readonly AuthenticatingRepositoryFactory repositoryFactory;
    readonly DeploymentScope deploymentScope;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly ILog log;
    readonly ICalamariFileSystem fileSystem;
    readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser;
    readonly ICommitMessageGenerator commitMessageGenerator;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

    public ApplicationUpdater(DeploymentScope deploymentScope,
                              UpdateArgoCDAppDeploymentConfig deploymentConfig,
                              AuthenticatingRepositoryFactory repositoryFactory,
                              ILog log,
                              ICalamariFileSystem fileSystem,
                              IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
                              ICommitMessageGenerator commitMessageGenerator,
                              ArgoCDOutputVariablesWriter outputVariablesWriter)
    {
        this.deploymentScope = deploymentScope;
        this.deploymentConfig = deploymentConfig;
        this.repositoryFactory = repositoryFactory;
        this.log = log;
        this.fileSystem = fileSystem;
        this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
        this.commitMessageGenerator = commitMessageGenerator;
        this.outputVariablesWriter = outputVariablesWriter;
    }
    
     public ProcessApplicationResult ProcessApplication(
            ArgoCDApplicationDto application,
            ArgoCDGatewayDto gateway)
        {
            log.InfoFormat("Processing application {0}", application.Name);
            var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);
            var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;
            var applicationName = applicationFromYaml.Metadata.Name;

            ValidateApplication(applicationFromYaml);
            
            LogHelmAnnotationWarning(applicationFromYaml);

            var repositoryAdapter = new RepositoryAdapter(repositoryFactory, deploymentConfig.CommitParameters, log, commitMessageGenerator);
            var sourceUpdater = new ApplicationSourceUpdater(applicationFromYaml, repositoryAdapter, deploymentScope, deploymentConfig, log, gateway, application.DefaultRegistry, outputVariablesWriter, fileSystem);
            
            var appliedSourcesResults = applicationFromYaml.GetSourcesWithMetadata()
                                                           .Where(sourceUpdater.IsAppInScope)
                                                           .Select(applicationSource => new
                                                           {
                                                               UpdateResult = sourceUpdater.ProcessSource(applicationSource),
                                                               applicationSource,
                                                           })
                                                           .ToList();

            //if we have links, use that to generate a link, otherwise just put the name there
            var instanceLinks = application.InstanceWebUiUrl != null ? new ArgoCDInstanceLinks(application.InstanceWebUiUrl) : null;
            var linkifiedAppName = instanceLinks != null
                ? log.FormatLink(instanceLinks.ApplicationDetails(applicationName, application.KubernetesNamespace), applicationName)
                : applicationName;

            var message = appliedSourcesResults.Any(r => r.UpdateResult.Updated)
                ? "Updated Application {0}"
                : "Nothing to update for Application {0}";

            log.InfoFormat(message, linkifiedAppName);

            return new ProcessApplicationResult(
                                                application.GatewayId,
                                                applicationName.ToApplicationName(),
                                                applicationFromYaml.Spec.Sources.Count,
                                                applicationFromYaml.Spec.Sources.Count(s => deploymentScope.Matches(ScopingAnnotationReader.GetScopeForApplicationSource(s.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources))),
                                                appliedSourcesResults.Select(r => new TrackedSourceDetail(r.UpdateResult.PushResult?.CommitSha, r.applicationSource.Index, [], r.UpdateResult.PatchedFiles)).ToList(),
                                                appliedSourcesResults.SelectMany(r => r.UpdateResult.ImagesUpdated).ToHashSet(),
                                                appliedSourcesResults.Where(r => r.UpdateResult.Updated).Select(r => r.applicationSource.Source.OriginalRepoUrl).ToHashSet());
        }

        void LogHelmAnnotationWarning(Application applicationFromYaml)
        {
            var imagesWithoutHelmValuePath = deploymentConfig.ImageReferences.Where(ir => ir.HelmReference.IsNullOrEmpty()).ToList();

            var someButNotAllHaveHelmValuePath = (imagesWithoutHelmValuePath.Count > 0) && (imagesWithoutHelmValuePath.Count < deploymentConfig.ImageReferences.Count);  
            
            if (someButNotAllHaveHelmValuePath && applicationFromYaml.GetSourcesWithMetadata().Any(src => src.SourceType == SourceType.Helm))
            {
                foreach (var image in imagesWithoutHelmValuePath)
                {
                    log.Verbose($"{image.ContainerReference.FriendlyName()} will not be updated in helm sources, as no helm yaml path has been specified for it in the step configuration.");
                }
            }
        }

        void ValidateApplication(Application applicationFromYaml)
        {
            var validationResult = ValidationResult.Merge(
                                                          ApplicationValidator.ValidateSourceNames(applicationFromYaml),
                                                          ApplicationValidator.ValidateUnnamedAnnotationsInMultiSourceApplication(applicationFromYaml),
                                                          ApplicationValidator.ValidateSourceTypes(applicationFromYaml)
                                                         );
            validationResult.Action(log);
        }
}
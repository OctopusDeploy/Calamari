using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ArgoCD;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class ApplicationUpdater
{
    readonly DeploymentScope deploymentScope;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly ILog log;
    readonly ICalamariFileSystem fileSystem;
    readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

    public ApplicationUpdater(DeploymentScope deploymentScope,
                              UpdateArgoCDAppDeploymentConfig deploymentConfig,
                              ILog log,
                              ICalamariFileSystem fileSystem,
                              IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
                              ArgoCDOutputVariablesWriter outputVariablesWriter)
    {
        this.deploymentScope = deploymentScope;
        this.deploymentConfig = deploymentConfig;
        this.log = log;
        this.fileSystem = fileSystem;
        this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
        this.outputVariablesWriter = outputVariablesWriter;
    }

    // Phase 1: parse, validate, scope and build the set of source updates for an application. The updates are
    // grouped (by repository/branch) across all applications and processed together by the GroupedRepositoryProcessor.
    public PlannedApplication Plan(ArgoCDApplicationDto application, ArgoCDGatewayDto gateway)
    {
        log.InfoFormat("Processing application {0}", application.Name);
        var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);
        var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;
        var applicationName = applicationFromYaml.Metadata.Name;
        var namespacedName = NamespacedApplicationName.Create(applicationName, applicationFromYaml.Metadata.Namespace);

        ValidateApplication(applicationFromYaml);
        LogHelmAnnotationWarning(applicationFromYaml);

        var sourceUpdater = new ApplicationSourceUpdater(applicationFromYaml, deploymentScope, deploymentConfig, log, application.DefaultRegistry, fileSystem);

        var plannedSources = applicationFromYaml.GetSourcesWithMetadata()
                                                .Where(sourceUpdater.IsAppInScope)
                                                .Select(source => new PlannedSource(source, new RepositorySourceUpdate(namespacedName, source, sourceUpdater.CreateSourceUpdater(source))))
                                                .ToList();

        var matchingSourceCount = applicationFromYaml.Spec.Sources.Count(s => deploymentScope.Matches(ScopingAnnotationReader.GetScopeForApplicationSource(s.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources)));

        return new PlannedApplication(application, gateway, applicationFromYaml, namespacedName, applicationName, plannedSources, applicationFromYaml.Spec.Sources.Count, matchingSourceCount);
    }

    // Phase 3: turn the processed results back into a per-application result, writing per-source output variables.
    public ProcessApplicationResult AssembleResult(PlannedApplication plan, IReadOnlyDictionary<RepositorySourceUpdate, SourceUpdateResult> resultsByUpdate)
    {
        foreach (var plannedSource in plan.Sources)
        {
            outputVariablesWriter.WriteSourceUpdateResultOutputWhenPushResultExists(plan.Gateway.Name,
                                                                                    plan.NamespacedName,
                                                                                    plannedSource.Source.Index,
                                                                                    resultsByUpdate[plannedSource.Update]);
        }

        var trackedSourceDetails = plan.Sources.Select(plannedSource =>
                                                        {
                                                            var result = resultsByUpdate[plannedSource.Update];
                                                            return new TrackedSourceDetail(result.PushResult?.CommitSha, result.PushResult?.CommitTimestamp, plannedSource.Source.Index, [], result.PatchedFiles);
                                                        })
                                               .ToList();

        //if we have links, use that to generate a link, otherwise just put the name there
        var instanceLinks = plan.Application.InstanceWebUiUrl != null ? new ArgoCDInstanceLinks(plan.Application.InstanceWebUiUrl) : null;
        var linkifiedAppName = instanceLinks != null
            ? log.FormatLink(instanceLinks.ApplicationDetails(plan.ApplicationName, plan.Application.KubernetesNamespace), plan.ApplicationName)
            : plan.ApplicationName;

        var anyUpdated = plan.Sources.Any(plannedSource => resultsByUpdate[plannedSource.Update].Updated);
        log.InfoFormat(anyUpdated ? "Updated Application {0}" : "Nothing to update for Application {0}", linkifiedAppName);

        return new ProcessApplicationResult(
                                            plan.Application.GatewayId,
                                            plan.NamespacedName,
                                            plan.TotalSourceCount,
                                            plan.MatchingSourceCount,
                                            trackedSourceDetails,
                                            plan.Sources.SelectMany(plannedSource => resultsByUpdate[plannedSource.Update].ImagesUpdated).ToHashSet(),
                                            plan.Sources.Where(plannedSource => resultsByUpdate[plannedSource.Update].Updated).Select(plannedSource => plannedSource.Source.Source.OriginalRepoUrl).ToHashSet());
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

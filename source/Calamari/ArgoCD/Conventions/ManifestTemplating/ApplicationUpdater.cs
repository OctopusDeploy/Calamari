using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Conventions.ManifestTemplating;

public class ApplicationUpdater
{
    readonly DeploymentScope deploymentScope;
    readonly ArgoCommitToGitConfig deploymentConfig;
    readonly ILog log;
    readonly ICalamariFileSystem fileSystem;
    readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;
    readonly IPackageRelativeFile[] packageFiles;

    public ApplicationUpdater(DeploymentScope deploymentScope,
                              ArgoCommitToGitConfig deploymentConfig,
                              ILog log,
                              ICalamariFileSystem fileSystem,
                              IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
                              ArgoCDOutputVariablesWriter outputVariablesWriter,
                              IPackageRelativeFile[] packageFiles)
    {
        this.deploymentScope = deploymentScope;
        this.deploymentConfig = deploymentConfig;
        this.log = log;
        this.fileSystem = fileSystem;
        this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
        this.outputVariablesWriter = outputVariablesWriter;
        this.packageFiles = packageFiles;
    }

    // Phase 1: parse, validate, scope and build the set of source updates for an application.
    public PlannedApplication Plan(ArgoCDApplicationDto application, ArgoCDGatewayDto gateway)
    {
        log.InfoFormat("Processing application {0}", application.Name);
        var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);
        var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;
        var applicationName = applicationFromYaml.Metadata.Name;
        var namespacedName = NamespacedApplicationName.Create(applicationName, applicationFromYaml.Metadata.Namespace);

        LogWarningIfUpdatingMultipleSources(applicationFromYaml.Spec.Sources,
                                            applicationFromYaml.Metadata.Annotations,
                                            containsMultipleSources);

        ValidateApplication(applicationFromYaml);

        var sourceUpdater = new ApplicationSourceUpdater(applicationFromYaml, deploymentScope, deploymentConfig, packageFiles, log, fileSystem);

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
                                                            return new TrackedSourceDetail(result.PushResult?.CommitSha, result.PushResult?.CommitTimestamp, plannedSource.Source.Index, result.ReplacedFiles, []);
                                                        })
                                               .ToList();

        //if we have links, use that to generate a link, otherwise just put the name there
        var instanceLinks = plan.Application.InstanceWebUiUrl != null ? new ArgoCDInstanceLinks(plan.Application.InstanceWebUiUrl) : null;
        var linkifiedAppName = instanceLinks != null
            ? log.FormatLink(instanceLinks.ApplicationDetails(plan.ApplicationName, plan.ApplicationFromYaml.Metadata.Namespace), plan.ApplicationName)
            : plan.ApplicationName;

        var anyUpdated = plan.Sources.Any(plannedSource => resultsByUpdate[plannedSource.Update].Updated);
        log.InfoFormat(anyUpdated ? "Updated Application {0}" : "Nothing to update for Application {0}", linkifiedAppName);

        return new ProcessApplicationResult(
                                            plan.Application.GatewayId,
                                            plan.NamespacedName,
                                            plan.TotalSourceCount,
                                            plan.MatchingSourceCount,
                                            trackedSourceDetails,
                                            [],
                                            plan.Sources.Where(plannedSource => resultsByUpdate[plannedSource.Update].Updated).Select(plannedSource => plannedSource.Source.Source.OriginalRepoUrl).ToHashSet());
    }

    void LogWarningIfUpdatingMultipleSources(
        List<ApplicationSource> sourcesToInspect,
        Dictionary<string, string> applicationAnnotations,
        bool containsMultipleSources)
    {
        if (sourcesToInspect.Count > 1)
        {
            var sourcesWithScopes = sourcesToInspect.Select(s => (s, ScopingAnnotationReader.GetScopeForApplicationSource(s.Name.ToApplicationSourceName(), applicationAnnotations, containsMultipleSources))).ToList();
            var sourcesWithMatchingScopes = sourcesWithScopes.Where(s => deploymentScope.Matches(s.Item2)).ToList();

            if (sourcesWithMatchingScopes.Count > 1)
            {
                log.Warn($"Multiple sources are associated with this deployment, they will all be updated with the same contents: {string.Join(", ", sourcesWithMatchingScopes.Select(s => s.s.Name))}");
            }
        }
    }

    void ValidateApplication(Application applicationFromYaml)
    {
        var validationResult = ValidationResult.Merge(
                                                      ApplicationValidator.ValidateSourceNames(applicationFromYaml),
                                                      ApplicationValidator.ValidateUnnamedAnnotationsInMultiSourceApplication(applicationFromYaml)
                                                     );
        validationResult.Action(log);
    }
}

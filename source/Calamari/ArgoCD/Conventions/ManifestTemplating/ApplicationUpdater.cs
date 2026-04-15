using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.ManifestTemplating;

public class ApplicationUpdater
{
    readonly AuthenticatingRepositoryFactory repositoryFactory;
    readonly DeploymentScope deploymentScope;
    readonly ArgoCommitToGitConfig deploymentConfig;
    readonly ILog log;
    readonly ICalamariFileSystem fileSystem;
    readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser;
    readonly ICommitMessageGenerator commitMessageGenerator;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;
    readonly IPackageRelativeFile[] packageFiles;
    

    public ApplicationUpdater(AuthenticatingRepositoryFactory repositoryFactory, DeploymentScope deploymentScope, ArgoCommitToGitConfig deploymentConfig, ILog log,
                              ICalamariFileSystem fileSystem,
                              IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
                              ArgoCDOutputVariablesWriter outputVariablesWriter,
                              IPackageRelativeFile[] packageFiles,
                              ICommitMessageGenerator commitMessageGenerator)
    {
        this.repositoryFactory = repositoryFactory;
        this.deploymentScope = deploymentScope;
        this.deploymentConfig = deploymentConfig;
        this.log = log;
        this.fileSystem = fileSystem;
        this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
        this.outputVariablesWriter = outputVariablesWriter;
        this.packageFiles = packageFiles;
        this.commitMessageGenerator = commitMessageGenerator;
    }
    
    public ProcessApplicationResult ProcessApplication(
        ArgoCDApplicationDto application,
        ArgoCDGatewayDto gateway)
    {
        log.InfoFormat("Processing application {0}", application.Name);
        var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);
        var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;
        var applicationName = applicationFromYaml.Metadata.Name;

        LogWarningIfUpdatingMultipleSources(applicationFromYaml.Spec.Sources,
                                            applicationFromYaml.Metadata.Annotations,
                                            containsMultipleSources);
        
        ValidateApplication(applicationFromYaml);

        var repositoryAdapter = new RepositoryAdapter(repositoryFactory, new RepositoryUpdater(deploymentConfig.CommitParameters, log, commitMessageGenerator));
        var sourceUpdater = new ApplicationSourceUpdater(applicationFromYaml,
                                                         gateway,
                                                         deploymentScope,
                                                         deploymentConfig,
                                                         packageFiles,
                                                         log,
                                                         fileSystem,
                                                         outputVariablesWriter,
                                                         repositoryAdapter);
        
        var trackedSourceUpdateResults = applicationFromYaml
                                    .GetSourcesWithMetadata()
                                    .Where(sourceUpdater.IsAppInScope)
                                    .Select(applicationSource => new
                                    {
                                        UpdateResult = sourceUpdater.ProcessSource(applicationSource),
                                        applicationSource
                                    })
                                    .ToList();

        //if we have links, use that to generate a link, otherwise just put the name there
        var instanceLinks = application.InstanceWebUiUrl != null ? new ArgoCDInstanceLinks(application.InstanceWebUiUrl) : null;
        var linkifiedAppName = instanceLinks != null
            ? log.FormatLink(instanceLinks.ApplicationDetails(applicationName, applicationFromYaml.Metadata.Namespace), applicationName)
            : applicationName;

        var message = trackedSourceUpdateResults.Any(u => u.UpdateResult.Updated)
            ? "Updated Application {0}"
            : "Nothing to update for Application {0}";

        log.InfoFormat(message, linkifiedAppName);

        return new ProcessApplicationResult(
                                            application.GatewayId,
                                            applicationName.ToApplicationName(),
                                            applicationFromYaml.Spec.Sources.Count,
                                            applicationFromYaml.Spec.Sources.Count(s => deploymentScope.Matches(ScopingAnnotationReader.GetScopeForApplicationSource(s.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources))),
                                            trackedSourceUpdateResults.Select(r => new TrackedSourceDetail(r.UpdateResult.CommitSha, r.UpdateResult.CommitTimestamp, r.applicationSource.Index, r.UpdateResult.ReplacedFiles, [])).ToList(),
                                            [],
                                            trackedSourceUpdateResults.Where(r => r.UpdateResult.Updated).Select(r => r.applicationSource.Source.OriginalRepoUrl).ToHashSet());
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

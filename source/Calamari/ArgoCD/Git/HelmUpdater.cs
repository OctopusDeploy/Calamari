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
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Git;

public class HelmUpdater : AbstractHelmUpdater
{
    readonly Application applicationFromYaml;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly string defaultRegistry;

    public HelmUpdater(Application applicationFromYaml,
                       UpdateArgoCDAppDeploymentConfig deploymentConfig,
                       string defaultRegistry,
                       ILog log,
                       ICalamariFileSystem fileSystem) : base(log,
                                                              fileSystem,
                                                              deploymentConfig,
                                                              defaultRegistry)
    {
        this.applicationFromYaml = applicationFromYaml;
        this.deploymentConfig = deploymentConfig;
        this.defaultRegistry = defaultRegistry;
    }

    public override FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        var applicationSource = sourceWithMetadata.Source;

        if (applicationSource.Path == null)
        {
            log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
            return new FileUpdateResult([], []);
        }

        if (deploymentConfig.HasStepBasedHelmValueReferences())
        {
            var appName = sourceWithMetadata.Source.Name.IsNullOrEmpty() ? null : new ApplicationSourceName(sourceWithMetadata.Source.Name);
            if (applicationFromYaml.Metadata.Annotations.ContainsKey(ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(appName)))
            {
                log.Warn($"Application '{applicationFromYaml.Metadata.Name}' specifies helm-value annotations which have been superseded by values specified in the step's configuration");
            }

            return ProcessHelmSourceUsingStepVariables(sourceWithMetadata, workingDirectory);
        }

        return ProcessHelmSourceUsingAnnotations(sourceWithMetadata, workingDirectory);
    }

    FileUpdateResult ProcessHelmSourceUsingAnnotations(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        var explicitHelmSources = new HelmValuesFileUpdateTargetParser(applicationFromYaml, defaultRegistry)
            .GetExplicitValuesFilesToUpdate(sourceWithMetadata);

        var valuesFilesToUpdate = new List<HelmValuesFileImageUpdateTarget>(explicitHelmSources.Targets);
        var valueFileProblems = new HashSet<HelmSourceConfigurationProblem>(explicitHelmSources.Problems);

        //Add the implicit value file if needed
        var repoSubPath = Path.Combine(workingDirectory, sourceWithMetadata.Source.Path!);
        var implicitValuesFile = HelmDiscovery.TryFindValuesFile(fileSystem, repoSubPath);
        if (implicitValuesFile != null && explicitHelmSources.Targets.None(t => t.FileName == implicitValuesFile))
        {
            var (target, problem) = AddImplicitValuesFile(sourceWithMetadata,
                                                          implicitValuesFile);
            if (target != null)
                valuesFilesToUpdate.Add(target);

            if (problem != null)
                valueFileProblems.Add(problem);
        }

        HelmHelpers.LogHelmSourceConfigurationProblems(log, valueFileProblems);

        return ProcessHelmUpdateTargets(workingDirectory,
                                        sourceWithMetadata,
                                        valuesFilesToUpdate);
    }

    (HelmValuesFileImageUpdateTarget? Target, HelmSourceConfigurationProblem? Problem) AddImplicitValuesFile(
        ApplicationSourceWithMetadata applicationSource,
        string valuesFilename)
    {
        var imageReplacePaths = ScopingAnnotationReader.GetImageReplacePathsForApplicationSource(
                                                                                                 applicationSource.Source.Name.ToApplicationSourceName(),
                                                                                                 applicationFromYaml.Metadata.Annotations,
                                                                                                 applicationFromYaml.Spec.Sources.Count > 1);
        if (!imageReplacePaths.Any())
        {
            return (null, new HelmSourceIsMissingImagePathAnnotation(applicationSource.SourceIdentity));
        }

        return (new HelmValuesFileImageUpdateTarget(defaultRegistry,
                                                    applicationSource.Source.Path,
                                                    valuesFilename,
                                                    imageReplacePaths), null);
    }

    FileUpdateResult ProcessHelmSourceUsingStepVariables(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        var extractor = new HelmValuesFileExtractor(applicationFromYaml);
        var valuesFilesInHelmSource = extractor.GetInlineValuesFilesReferencedByHelmSource(sourceWithMetadata);

        var filesToUpdate = valuesFilesInHelmSource.Select(file => Path.Combine(workingDirectory, file)).ToList();
        var implicitValuesFile = HelmDiscovery.TryFindValuesFile(fileSystem, Path.Combine(workingDirectory, sourceWithMetadata.Source.Path!));
        if (implicitValuesFile != null)
        {
            implicitValuesFile = Path.Combine(workingDirectory, sourceWithMetadata.Source.Path!, implicitValuesFile);
            filesToUpdate.Add(implicitValuesFile);
        }

        filesToUpdate = filesToUpdate.Select(file => Path.Combine(workingDirectory, file)).ToList();
        var result = ProcessHelmValuesFiles(filesToUpdate.ToHashSet(),
                                            workingDirectory,
                                            sourceWithMetadata);
        return result;
    }
}
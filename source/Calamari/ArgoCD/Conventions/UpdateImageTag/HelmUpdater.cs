using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class HelmUpdater : AbstractHelmUpdater
{
    readonly string defaultRegistry;

    public HelmUpdater(Application applicationFromYaml,
                       UpdateArgoCDAppDeploymentConfig deploymentConfig,
                       string defaultRegistry,
                       ILog log,
                       ICalamariFileSystem fileSystem) : base(log,
                                                              fileSystem,
                                                              applicationFromYaml,
                                                              deploymentConfig,
                                                              defaultRegistry)
    {
        this.defaultRegistry = defaultRegistry;
    }

    protected override bool ValidateSource(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        if (sourceWithMetadata.Source.Path == null)
        {
            log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceWithMetadata.SourceIdentity);
            return false;
        }
        return true;
    }

    protected override IReadOnlyCollection<HelmValuesFileTarget> GetStepVariableFileTargets(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        var extractor = new HelmValuesFileExtractor(applicationFromYaml);
        var relativePaths = new HashSet<string>(extractor.GetInlineValuesFilesReferencedByHelmSource(sourceWithMetadata));

        var implicitValuesFile = HelmDiscovery.TryFindValuesFile(fileSystem, Path.Combine(workingDirectory, sourceWithMetadata.Source.Path!));
        if (implicitValuesFile != null)
        {
            relativePaths.Add(Path.Combine(sourceWithMetadata.Source.Path!, implicitValuesFile));
        }

        return relativePaths.Select(f => new HelmValuesFileTarget(f)).ToList();
    }

    protected override (IReadOnlyCollection<HelmValuesFileTarget> Targets, IReadOnlyCollection<HelmSourceConfigurationProblem> Problems) GetAnnotationFileTargets(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        var explicitHelmSources = new HelmValuesFileUpdateTargetParser(applicationFromYaml, defaultRegistry)
            .GetExplicitValuesFilesToUpdate(sourceWithMetadata);

        var valuesFilesToUpdate = new List<HelmValuesFileImageUpdateTarget>(explicitHelmSources.Targets);
        var valueFileProblems = new HashSet<HelmSourceConfigurationProblem>(explicitHelmSources.Problems);

        var repoSubPath = Path.Combine(workingDirectory, sourceWithMetadata.Source.Path!);
        var implicitValuesFile = HelmDiscovery.TryFindValuesFile(fileSystem, repoSubPath);
        if (implicitValuesFile != null && explicitHelmSources.Targets.None(t => t.FileName == implicitValuesFile))
        {
            var (target, problem) = AddImplicitValuesFile(sourceWithMetadata, implicitValuesFile);
            if (target != null)
                valuesFilesToUpdate.Add(target);
            if (problem != null)
                valueFileProblems.Add(problem);
        }

        return (valuesFilesToUpdate.Select(HelmValuesFileTarget.FromAnnotationTarget).ToList(), valueFileProblems);
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
            return (null, new HelmSourceIsMissingImagePathAnnotation(applicationSource.SourceIdentity));

        return (new HelmValuesFileImageUpdateTarget(defaultRegistry,
                                                    applicationSource.Source.Path,
                                                    valuesFilename,
                                                    imageReplacePaths), null);
    }
}

using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class RefUpdater : AbstractHelmUpdater
{
    readonly string defaultRegistry;

    public RefUpdater(Application applicationFromYaml,
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
        if (sourceWithMetadata.Source.Path != null)
        {
            log.WarnFormat("The source '{0}' contains a Ref, only referenced files will be updated. Please create another source with the same URL if you wish to update files under the path.", sourceWithMetadata.SourceIdentity);
        }
        return true; // ref sources are always valid
    }

    protected override IReadOnlyCollection<HelmValuesFileTarget> GetStepVariableFileTargets(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        var extractor = new HelmValuesFileExtractor(applicationFromYaml);
        return extractor.GetValueFilesReferencedInRefSource(sourceWithMetadata)
                        .Select(file => new HelmValuesFileTarget(file))
                        .ToList();
    }

    protected override (IReadOnlyCollection<HelmValuesFileTarget> Targets, IReadOnlyCollection<HelmSourceConfigurationProblem> Problems) GetAnnotationFileTargets(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        var helmTargetsForRefSource = new HelmValuesFileUpdateTargetParser(applicationFromYaml, defaultRegistry)
            .GetHelmTargetsForRefSource(sourceWithMetadata);

        return (helmTargetsForRefSource.Targets.Select(HelmValuesFileTarget.FromAnnotationTarget).ToList(),
                helmTargetsForRefSource.Problems);
    }
}

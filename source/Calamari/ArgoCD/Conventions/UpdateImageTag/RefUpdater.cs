using System;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class RefUpdater : AbstractHelmUpdater
{
    readonly Application applicationFromYaml;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly string defaultRegistry;

    public RefUpdater(Application applicationFromYaml,
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

        if (applicationSource.Path != null)
        {
            log.WarnFormat("The source '{0}' contains a Ref, only referenced files will be updated. Please create another source with the same URL if you wish to update files under the path.", sourceWithMetadata.SourceIdentity);
        }

        if (deploymentConfig.HasStepBasedHelmValueReferences())
        {
            if (applicationFromYaml.Metadata.Annotations.ContainsKey(ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(new ApplicationSourceName(sourceWithMetadata.Source.Name))))
            {
                log.Warn($"Application {applicationFromYaml.Metadata.Name} specifies helm-value annotations which have been superseded by container-values specified in the step's configuration");
            }

            return ProcessRefSourceUsingStepVariables(sourceWithMetadata, workingDirectory);
        }

        var helmTargetsForRefSource = new HelmValuesFileUpdateTargetParser(applicationFromYaml, defaultRegistry)
            .GetHelmTargetsForRefSource(sourceWithMetadata);

        HelmHelpers.LogHelmSourceConfigurationProblems(log, helmTargetsForRefSource.Problems);
        WarnIfNothingReferencedRefSource(sourceWithMetadata, helmTargetsForRefSource.Targets.Count, helmTargetsForRefSource.Problems.Count);

        return ProcessHelmUpdateTargets(workingDirectory,
                                        helmTargetsForRefSource.Targets);
    }

    FileUpdateResult ProcessRefSourceUsingStepVariables(ApplicationSourceWithMetadata sourceWithMetadata,
                                                        string workingDirectory)
    {
        var extractor = new HelmValuesFileExtractor(applicationFromYaml);
        var valuesFiles = extractor.GetValueFilesReferencedInRefSource(sourceWithMetadata)
                                   .Select(file => Path.Combine(workingDirectory, file))
                                   .ToHashSet();
        WarnIfNothingReferencedRefSource(sourceWithMetadata, valuesFiles.Count, 0);

        return ProcessHelmValuesFiles(valuesFiles,
                                      workingDirectory,
                                      sourceWithMetadata);
    }

    void WarnIfNothingReferencedRefSource(ApplicationSourceWithMetadata sourceWithMetadata, int targetCount, int problemCount)
    {
        if (targetCount == 0 && problemCount == 0)
            log.WarnFormat("Ref source '{0}' was processed but no value files in the application referenced it (via ${1}/...), so nothing was updated.", sourceWithMetadata.SourceIdentity, sourceWithMetadata.Source.Ref);
    }
}
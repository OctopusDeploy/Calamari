using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Patching.JsonPatch;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public abstract class AbstractHelmUpdater : ISourceUpdater
{
    protected readonly ILog log;
    protected readonly ICalamariFileSystem fileSystem;
    protected readonly Application applicationFromYaml;
    readonly UpdateArgoCDAppDeploymentConfig deploymentConfig;
    readonly string defaultRegistry;

    protected AbstractHelmUpdater(ILog log,
                              ICalamariFileSystem fileSystem,
                              Application applicationFromYaml,
                              UpdateArgoCDAppDeploymentConfig deploymentConfig,
                              string defaultRegistry)
    {
        this.log = log;
        this.fileSystem = fileSystem;
        this.applicationFromYaml = applicationFromYaml;
        this.deploymentConfig = deploymentConfig;
        this.defaultRegistry = defaultRegistry;
    }

    public FileUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory)
    {
        if (!ValidateSource(sourceWithMetadata))
            return new FileUpdateResult([], [], [], []);

        IReadOnlyCollection<HelmValuesFileTarget> targets;
        if (deploymentConfig.HasStepBasedHelmValueReferences())
        {
            WarnIfAnnotationsSuperseded(sourceWithMetadata);
            targets = GetStepVariableFileTargets(sourceWithMetadata, workingDirectory);
        }
        else
        {
            (targets, var problems) = GetAnnotationFileTargets(sourceWithMetadata, workingDirectory);
            LogHelmSourceConfigurationProblems(log, problems);
        }
        
        return ProcessHelmValuesFiles(workingDirectory, targets);
    }

    protected abstract bool ValidateSource(ApplicationSourceWithMetadata sourceWithMetadata);
    protected abstract IReadOnlyCollection<HelmValuesFileTarget> GetStepVariableFileTargets(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory);
    protected abstract (IReadOnlyCollection<HelmValuesFileTarget> Targets, IReadOnlyCollection<HelmSourceConfigurationProblem> Problems) GetAnnotationFileTargets(ApplicationSourceWithMetadata sourceWithMetadata, string workingDirectory);

    void WarnIfAnnotationsSuperseded(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        var appName = string.IsNullOrEmpty(sourceWithMetadata.Source.Name) ? null : new ApplicationSourceName(sourceWithMetadata.Source.Name);
        if (applicationFromYaml.Metadata.Annotations.ContainsKey(ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(appName)))
        {
            log.Warn($"Application '{applicationFromYaml.Metadata.Name}' specifies helm-value annotations which have been superseded by values specified in the step's configuration");
        }
    }

    FileUpdateResult ProcessHelmValuesFiles(
        string workingDirectory,
        IReadOnlyCollection<HelmValuesFileTarget> files)
    {
        var converter = new HelmAnnotationToReferenceConverter(defaultRegistry, log);
        var updatedImages = new HashSet<string>();
        var jsonPatches = new List<FileJsonPatch>();

        foreach (var file in files)
        {
            var filepath = Path.Combine(workingDirectory, file.RelativePath);
            var fileContent = fileSystem.ReadFile(filepath);

            var imageReferences = file.AnnotationTemplates != null
                ? converter.Resolve(fileContent, file.AnnotationTemplates, deploymentConfig.ImageReferences)
                : deploymentConfig.ImageReferences;

            if (imageReferences.Count == 0)
                continue;

            ProcessFile(filepath, file.RelativePath, fileContent, imageReferences, updatedImages, jsonPatches);
        }

        return new FileUpdateResult(updatedImages, [], jsonPatches, []);
    }

    void ProcessFile(
        string filepath,
        string relativePath,
        string fileContent,
        IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imageReferences,
        HashSet<string> updatedImages,
        List<FileJsonPatch> jsonPatches)
    {
        log.Info($"Processing file at {filepath}.");

        var replacer = new HelmContainerImageReplacer(fileContent, defaultRegistry, log);
        var result = replacer.UpdateImages(imageReferences);

        if (result.UpdatedImageReferences.Count > 0)
        {
            fileSystem.OverwriteFile(filepath, result.UpdatedContents);
            updatedImages.UnionWith(result.UpdatedImageReferences);
        }

        var patch = CreateJsonPatchWithPlaceholders(fileContent, imageReferences);
        if (patch != null)
            jsonPatches.Add(new FileJsonPatch(relativePath, JsonSerializer.Serialize(patch)));
    }

    JsonPatchDocument? CreateJsonPatchWithPlaceholders(
        string content,
        IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imageReferences)
    {
        var placeholderImages = imageReferences
            .Select(ir =>
            {
                var placeholderRef = MakePlaceholderRef(ir.ContainerReference.FriendlyName());
                return ir with { ContainerReference = ContainerImageReference.FromReferenceString(placeholderRef, defaultRegistry) };
            })
            .ToList();

        var placeholderResult = new HelmContainerImageReplacer(content, defaultRegistry, log).UpdateImages(placeholderImages);
        if (placeholderResult.UpdatedImageReferences.Count == 0)
            return null;

        var temporaryBefore = placeholderResult.UpdatedContents;
        var actualResult = new HelmContainerImageReplacer(temporaryBefore, defaultRegistry, log).UpdateImages(imageReferences);

        return actualResult.UpdatedImageReferences.Count > 0
            ? JsonPatchUtils.CreateJsonPatchFromDiff(temporaryBefore, actualResult.UpdatedContents)
            : null;
    }
    
    static void LogHelmSourceConfigurationProblems(ILog log, IReadOnlyCollection<HelmSourceConfigurationProblem> helmSourceConfigurationProblems)
    {
        foreach (var helmSourceConfigurationProblem in helmSourceConfigurationProblems)
        {
            LogProblem(helmSourceConfigurationProblem);
        }

        void LogProblem(HelmSourceConfigurationProblem helmSourceConfigurationProblem)
        {
            switch (helmSourceConfigurationProblem)
            {
                case HelmSourceIsMissingImagePathAnnotation helmSourceIsMissingImagePathAnnotation:
                {
                    if (helmSourceIsMissingImagePathAnnotation.RefSourceIdentity == null)
                    {
                        log.WarnFormat("The Helm source '{0}' is missing an annotation for the image replace path. It will not be updated.",
                                       helmSourceIsMissingImagePathAnnotation.SourceIdentity);
                    }
                    else
                    {
                        log.WarnFormat("The Helm source '{0}' is missing an annotation for the image replace path. The source '{1}' will not be updated.",
                                       helmSourceIsMissingImagePathAnnotation.SourceIdentity,
                                       helmSourceIsMissingImagePathAnnotation.RefSourceIdentity);
                    }

                    log.WarnFormat("Annotation creation documentation can be found {0}.", log.FormatShortLink("argo-cd-helm-image-annotations", "here"));

                    return;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(helmSourceConfigurationProblem));
            }
        }
    }
}

public record HelmValuesFileTarget(string RelativePath, IReadOnlyCollection<string>? AnnotationTemplates = null)
{
    public static HelmValuesFileTarget FromAnnotationTarget(HelmValuesFileImageUpdateTarget target)
        => new(Path.Combine(target.Path, target.FileName), target.ImagePathDefinitions);
}

using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Helm;

/// <summary>
/// Converts annotation-based image replacement path templates into HelmReference entries
/// so that both annotation and step-variable paths can use the same replacement logic.
/// </summary>
public class HelmAnnotationToReferenceConverter
{
    readonly string defaultClusterRegistry;
    readonly ILog log;

    public HelmAnnotationToReferenceConverter(string defaultClusterRegistry, ILog log)
    {
        this.defaultClusterRegistry = defaultClusterRegistry;
        this.log = log;
    }

    /// <summary>
    /// Resolves annotation templates against the given YAML content and matches them to
    /// the incoming images. Returns image references with HelmReference set to the resolved
    /// YAML dot-notation path, suitable for use with HelmContainerImageReplacer.
    /// </summary>
    public IReadOnlyCollection<ContainerImageReferenceAndHelmReference> Resolve(
        string yamlContent,
        IReadOnlyCollection<string> annotationTemplates,
        IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var yamlParser = new HelmYamlParser(yamlContent);
        var variableDictionary = HelmValuesEditor.GenerateVariableDictionary(yamlParser);

        if (variableDictionary.GetNames().Count == 0)
            return [];

        var resolvedReferences = new List<ContainerImageReferenceAndHelmReference>();

        foreach (var template in annotationTemplates)
        {
            var templatedPath = TemplatedImagePath.Parse(template, variableDictionary, defaultClusterRegistry);

            log.Verbose($"Resolved annotation template '{template}' to path '{templatedPath.TagPath}'");

            var matchedImage = imagesToUpdate
                .Select(i => new { Image = i, Comparison = i.ContainerReference.CompareWith(templatedPath.ImageReference) })
                .FirstOrDefault(i => i.Comparison.MatchesImage());

            if (matchedImage != null)
            {
                resolvedReferences.Add(matchedImage.Image with { HelmReference = templatedPath.TagPath });
            }
        }

        return resolvedReferences;
    }
}

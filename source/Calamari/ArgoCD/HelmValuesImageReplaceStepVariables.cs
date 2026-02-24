using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Helm;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD;

public class HelmValuesImageReplaceStepVariables : IContainerImageReplacer
{
    readonly string yamlContent;
    readonly string defaultRegistry;
    readonly ILog log;

    public HelmValuesImageReplaceStepVariables(string yamlContent, string defaultRegistry, ILog log)
    {
        this.yamlContent = yamlContent;
        this.defaultRegistry = defaultRegistry;
        this.log = log;
    }

    public ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
    {
        var imagesUpdated = new HashSet<string>();
        var updatedYaml = yamlContent;
        var originalYamlParser = new HelmYamlParser(yamlContent); // Parse and track the original yaml so that content can be read from it.
        var flattenedYamlPathDictionary = HelmValuesEditor.GenerateVariableDictionary(originalYamlParser);

        foreach (var newImageTag in imagesToUpdate.Where(c => c.HelmReference is not null))
        {
            var helmReference = newImageTag.HelmReference!;
            var valueToUpdate = flattenedYamlPathDictionary.GetRaw(helmReference);
            if (valueToUpdate == null)
            {
                log.Verbose($"{helmReference} for image {newImageTag.ContainerReference.ToString()} was not found in your values file.");
                continue;
            }
                
            if (IsUnstructuredText(valueToUpdate))
            {
                HelmValuesEditor.UpdateNodeValue(updatedYaml, helmReference, newImageTag.ContainerReference.Tag);
            }
            else
            {
                var cir = ContainerImageReference.FromReferenceString(valueToUpdate, defaultRegistry);
                var comparison = newImageTag.ContainerReference.CompareWith(cir);
                if (comparison.MatchesImage())
                {
                    if (!comparison.TagMatch)
                    {
                        var newValue = cir.WithTag(newImageTag.ContainerReference.Tag);
                        updatedYaml = HelmValuesEditor.UpdateNodeValue(updatedYaml, helmReference, newValue);
                        imagesUpdated.Add(newValue);
                    }
                }
                else
                {
                    log.Warn($"Attempted to update value entry '{helmReference}', however it contains a mismatched image name and registry.");
                }
            }
        }
        return new ImageReplacementResult(updatedYaml, imagesUpdated);
    }

    bool IsUnstructuredText(string content)
        {
            var lastColonIndex = content.LastIndexOf(':');
            var lastSlashIndex = content.LastIndexOf('/');

            return lastColonIndex == -1 && lastSlashIndex == -1;
        }
}
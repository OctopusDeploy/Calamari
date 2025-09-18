#if NET
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Helm
{
    public class HelmContainerImageReplacer
    {
        readonly string yamlContent;
        readonly string defaultClusterRegistry;
        readonly List<string> imagePathAnnotations;

        public HelmContainerImageReplacer(string yamlContent, string defaultClusterRegistry, List<string> imagePathAnnotations)
        {
            this.yamlContent = yamlContent;
            this.defaultClusterRegistry = defaultClusterRegistry;
            this.imagePathAnnotations = imagePathAnnotations;
        }

        // TODO: Add testing for multiple instances of the same image
        public ImageReplacementResult UpdateImages(List<ContainerImageReference> imagesToUpdate)
        {
            var updatedImages = new HashSet<string>();

            var originalYamlParser = new HelmYamlParser(yamlContent); // Parse and track the original yaml so that content can be read from it.

            var imagePathDictionary = HelmValuesEditor.GenerateVariableDictionary(originalYamlParser);
            var existingImageReferences = imagePathAnnotations.Select(p => TemplatedImagePath.Parse(p, imagePathDictionary, defaultClusterRegistry));

            var fileContent = yamlContent;
            foreach (var existingImageReference in existingImageReferences)
            {
                var matchedUpdate = imagesToUpdate.FirstOrDefault(i => i.IsMatch(existingImageReference.ImageReference));
                if (!(matchedUpdate is null) && !matchedUpdate.Tag.Equals(existingImageReference.ImageReference.Tag, StringComparison.OrdinalIgnoreCase))
                {
                    if (existingImageReference.TagIsTemplateToken)
                    {
                        // If the tag is specified separately in its own node
                        fileContent = HelmValuesEditor.UpdateNodeValue(fileContent, existingImageReference.TagPath, matchedUpdate.Tag);
                    }
                    else
                    {
                        // We re-read the node value with the image details so we can ensure we only write out the image ref components expected
                        var imageTagNodeValue = originalYamlParser.GetValueAtPath(existingImageReference.TagPath);
                        var replacementImageRef = ContainerImageReference.FromReferenceString(imageTagNodeValue, defaultClusterRegistry).WithTag(matchedUpdate.Tag);
                        fileContent = HelmValuesEditor.UpdateNodeValue(fileContent, existingImageReference.TagPath, replacementImageRef);
                    }

                    updatedImages.Add(matchedUpdate.ToString());
                }
            }

            return new ImageReplacementResult(fileContent, updatedImages);
        }
    }
}
#endif
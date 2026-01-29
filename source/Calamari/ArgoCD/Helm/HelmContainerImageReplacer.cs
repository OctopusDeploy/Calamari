using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Helm
{
    public class HelmContainerImageReplacer
    {
        readonly string yamlContent;
        readonly string defaultClusterRegistry;
        readonly IReadOnlyCollection<string> imagePathAnnotations;
        readonly ILog log;

        public HelmContainerImageReplacer(string yamlContent, string defaultClusterRegistry, IReadOnlyCollection<string> imagePathAnnotations, ILog log)
        {
            this.yamlContent = yamlContent;
            this.defaultClusterRegistry = defaultClusterRegistry;
            this.imagePathAnnotations = imagePathAnnotations;
            this.log = log;
        }

        // TODO: Add testing for multiple instances of the same image
        public ImageReplacementResult UpdateImages(List<ContainerImageReference> imagesToUpdate)
        {
            var updatedImages = new HashSet<string>();

            var originalYamlParser = new HelmYamlParser(yamlContent); // Parse and track the original yaml so that content can be read from it.

            var imagePathDictionary = HelmValuesEditor.GenerateVariableDictionary(originalYamlParser);
            var existingImageReferences = imagePathAnnotations.Select(p => TemplatedImagePath.Parse(p, imagePathDictionary, defaultClusterRegistry)).ToList();
            
            var fileContent = yamlContent;
            foreach (var existingImageReference in existingImageReferences)
            {
                log.Verbose($"Apply template {existingImageReference.TagPath}, {existingImageReference.ImageReference.ToString()}");
                var imagesString = imagesToUpdate.Select(i => i.ToString());
                log.Verbose($"Images to Update = {string.Join(",", imagesString)}");
                
                var matchedUpdate = imagesToUpdate.Select(i => new
                                                  {
                                                      Reference = i,
                                                      Comparison = i.CompareWith(existingImageReference.ImageReference) 
                                                      
                                                  })
                                                  .FirstOrDefault(i => i.Comparison.IsImageMatch());
                
                //var matchedUpdate = imagesToUpdate.FirstOrDefault(i => i.IsMatch(existingImageReference.ImageReference));
                if (matchedUpdate != null && !matchedUpdate.Comparison.TagMatch)
                {
                    if (existingImageReference.TagIsTemplateToken)
                    {
                        // If the tag is specified separately in its own node
                        fileContent = HelmValuesEditor.UpdateNodeValue(fileContent, existingImageReference.TagPath, matchedUpdate.Reference.Tag);
                    }
                    else
                    {
                        // We re-read the node value with the image details so we can ensure we only write out the image ref components expected
                        var imageTagNodeValue = originalYamlParser.GetValueAtPath(existingImageReference.TagPath);
                        var replacementImageRef = ContainerImageReference.FromReferenceString(imageTagNodeValue, defaultClusterRegistry).WithTag(matchedUpdate.Reference.Tag);
                        fileContent = HelmValuesEditor.UpdateNodeValue(fileContent, existingImageReference.TagPath, replacementImageRef);
                    }

                    updatedImages.Add(matchedUpdate.ToString());
                }
            }

            return new ImageReplacementResult(fileContent, updatedImages);
        }
    }
}


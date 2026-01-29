#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.ArgoCD.Models;
using k8s;
using k8s.Models;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD
{
    public class ContainerImageReplacer : IContainerImageReplacer
    {
        readonly string yamlContent;
        readonly string defaultRegistry;

        public ContainerImageReplacer(string yamlContent, string defaultRegistry)
        {
            this.yamlContent = yamlContent;
            this.defaultRegistry = defaultRegistry;
        }

        public ImageReplacementResult UpdateImages(List<ContainerImageReference> imagesToUpdate)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                return new ImageReplacementResult(yamlContent, new HashSet<string>());
            }

            var documents = yamlContent.SplitYamlDocuments();

            var updatedDocuments = new List<string>();
            var imageReplacements = new HashSet<string>();

            foreach (var document in documents)
            {
                // Try parse YAML to extract 'kind'
                var yamlStream = new YamlStream();
                try
                {
                    yamlStream.Load(new StringReader(document));
                }
                catch
                {
                    // Invalid YAML — leave as-is
                    updatedDocuments.Add(document);
                    continue;
                }

                // If we don't have any documents or the first document is not a mapping with a 'kind' field (i.e. a k8s resource), skip
                if (yamlStream.Documents.Count == 0 || !(yamlStream.Documents[0].RootNode is YamlMappingNode rootNode) || !IsPotentialKubernetesResource(rootNode))
                {
                    updatedDocuments.Add(document);
                    continue;
                }

                try
                {
                    var resources = KubernetesYaml.LoadAllFromString(document.RemoveDocumentSeparators()); // we remove trailing --- to avoid issues with deserialization, and we do it with regex so we can account for newline values etc
                    if (resources == null || resources.Count == 0)
                    {
                        updatedDocuments.Add(document);
                        continue;
                    }

                    if (resources.Count > 1)
                    {
                        // We're splitting YAML documents, so we expect only one resource per document
                        throw new InvalidDataException($"Expected single resources in YAML document, but found {resources.Count}.");
                    }

                    var resource = resources[0];
                    var (updatedDocument, changes) = UpdateImagesInKubernetesResource(document, resource, imagesToUpdate);
                    imageReplacements.UnionWith(changes);
                    // NOTE: We don't need to check if a change has been made or not, if it hasn't, the final document will remain unchanged.
                    updatedDocuments.Add(updatedDocument);
                }
                catch
                {
                    // If deserialization fails, skip
                    updatedDocuments.Add(document);
                }
            }

            // Stitch documents back together, trailing --- will remain in places for valid yaml
            return new ImageReplacementResult(string.Concat(updatedDocuments), imageReplacements);
        }

        (string, HashSet<string>) UpdateImagesInKubernetesResource(string initialDocument, object? resourceObject, List<ContainerImageReference> imagesToUpdate)
        {
            var updatedDocument = initialDocument;
            var imageReplacements = new HashSet<string>();

            List<string> replacementResult;

            switch (resourceObject)
            {
                case V1Pod pod:
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, pod.Spec?.Containers);
                    imageReplacements.UnionWith(replacementResult);
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, pod.Spec?.InitContainers);
                    imageReplacements.UnionWith(replacementResult);
                    break;

                case V1Deployment deployment:
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, deployment.Spec?.Template?.Spec?.Containers);
                    imageReplacements.UnionWith(replacementResult);
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, deployment.Spec?.Template?.Spec?.InitContainers);
                    imageReplacements.UnionWith(replacementResult);
                    break;

                case V1StatefulSet statefulSet:
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, statefulSet.Spec?.Template?.Spec?.Containers);
                    imageReplacements.UnionWith(replacementResult);
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, statefulSet.Spec?.Template?.Spec?.InitContainers);
                    imageReplacements.UnionWith(replacementResult);
                    break;

                case V1DaemonSet daemonSet:
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, daemonSet.Spec?.Template?.Spec?.Containers);
                    imageReplacements.UnionWith(replacementResult);
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, daemonSet.Spec?.Template?.Spec?.InitContainers);
                    imageReplacements.UnionWith(replacementResult);
                    break;

                case V1Job job:
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, job.Spec?.Template?.Spec?.Containers);
                    imageReplacements.UnionWith(replacementResult);
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, job.Spec?.Template?.Spec?.InitContainers);
                    imageReplacements.UnionWith(replacementResult);
                    break;

                case V1CronJob cronJob:
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, cronJob.Spec?.JobTemplate?.Spec?.Template?.Spec?.Containers);
                    imageReplacements.UnionWith(replacementResult);
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, cronJob.Spec?.JobTemplate?.Spec?.Template?.Spec?.InitContainers);
                    imageReplacements.UnionWith(replacementResult);
                    break;

                case V1ReplicationController rc:
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, rc.Spec?.Template?.Spec?.Containers);
                    imageReplacements.UnionWith(replacementResult);
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, rc.Spec?.Template?.Spec?.InitContainers);
                    imageReplacements.UnionWith(replacementResult);
                    break;

                case V1ReplicaSet rs:
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, rs.Spec?.Template?.Spec?.Containers);
                    imageReplacements.UnionWith(replacementResult);
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, rs.Spec?.Template?.Spec?.InitContainers);
                    imageReplacements.UnionWith(replacementResult);
                    break;

                case V1PodTemplate podTemplate:
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, podTemplate.Template.Spec.Containers);
                    imageReplacements.UnionWith(replacementResult);
                    (updatedDocument, replacementResult) = ReplaceImageReferences(updatedDocument, imagesToUpdate, podTemplate.Template.Spec.InitContainers);
                    imageReplacements.UnionWith(replacementResult);
                    break;
            }


            return (updatedDocument, imageReplacements);
        }

        (string, List<string>) ReplaceImageReferences(string document, List<ContainerImageReference> imagesToUpdate, IList<V1Container>? containers)
        {
            if (containers == null || containers.Count == 0)
            {
                return (document, new List<string>());
            }

            var replacementsMade = new List<string>();

            foreach (var container in containers)
            {
                var currentReference = ContainerImageReference.FromReferenceString(container.Image, defaultRegistry);

                var matchedUpdate = imagesToUpdate.Select(i => new
                                                  {
                                                      Reference = i,
                                                      Comparison = i.CompareWith(currentReference) 
                                                      
                                                  })
                                                  .FirstOrDefault(i => i.Comparison.MatchesImage());
                if (matchedUpdate != null)
                {
                    // Only do replacement if the tag is different
                    if (!matchedUpdate.Comparison.TagMatch)
                    {
                        var newReference = currentReference.WithTag(matchedUpdate.Reference.Tag);

                        // Pattern ensures we only update lines with  `image: <IMAGENAME>` OR  `- image: <IMAGENANME>`.
                        // Ignores comments and white space, while preserving any quotes around the image name 
                        var pattern = $@"(?<=^\s*-?\s*image:\s*)([""']?){Regex.Escape(container.Image)}\1(?=\s*(#.*)?$)";
                        document = Regex.Replace(document,
                                                 pattern,
                                                 match =>
                                                 {
                                                     var quote = match.Groups[1].Value; // quote char or empty
                                                     // Wrap newReference in the original quotes if any
                                                     return $"{quote}{newReference}{quote}";
                                                 },
                                                 RegexOptions.Multiline);

                        replacementsMade.Add($"{matchedUpdate.Reference.ImageName}:{matchedUpdate.Reference.Tag}");
                    }
                }
            }

            return (document, replacementsMade);
        }

        static bool IsPotentialKubernetesResource(YamlMappingNode rootNode)
        {
            // Check if kind and API version are present and not empty strings
            return rootNode.Children.TryGetValue(new YamlScalarNode("kind"), out var kindNode) && kindNode is YamlScalarNode kindScalar && !string.IsNullOrWhiteSpace(kindScalar.Value) && rootNode.Children.TryGetValue(new YamlScalarNode("apiVersion"), out var apiVersionNode) && apiVersionNode is YamlScalarNode apiVersionScalar && !string.IsNullOrWhiteSpace(apiVersionScalar.Value);
        }
    }
}


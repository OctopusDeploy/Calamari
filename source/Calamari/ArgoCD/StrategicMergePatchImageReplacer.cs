#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD
{
    public class StrategicMergePatchImageReplacer : IContainerImageReplacer
    {
        private static class FieldNames
        {
            public const string Containers = "containers";
            public const string InitContainers = "initContainers";
            public const string Image = "image";
        }

        readonly string yamlContent;
        readonly string defaultRegistry;
        readonly ILog log;

        public StrategicMergePatchImageReplacer(string yamlContent, string defaultRegistry, ILog log)
        {
            this.yamlContent = yamlContent;
            this.defaultRegistry = defaultRegistry;
            this.log = log;
        }

        ImageReplacementResult NoChangeResult => new ImageReplacementResult(yamlContent, new HashSet<string>(), new HashSet<string>());

        public ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                log.Warn("Strategic merge patch file content is empty or whitespace only.");
                return NoChangeResult;
            }

            var stream = YamlStreamLoader.TryLoad(yamlContent, log, "Strategic merge patch");
            if (stream == null) return NoChangeResult;
            var documents = stream.Documents.ToList();
            if (documents.Count == 0) return NoChangeResult;

            var (modifiedDocuments, replacementsMade) = ProcessAllDocuments(documents, imagesToUpdate);

            if (replacementsMade.Count == 0) return NoChangeResult;

            var modifiedYaml = YamlStreamLoader.SerializeDocuments(modifiedDocuments, yamlContent);
            return new ImageReplacementResult(modifiedYaml, replacementsMade, new HashSet<string>());
        }


        private (List<YamlDocument> documents, HashSet<string> replacements) ProcessAllDocuments(
            List<YamlDocument> documents,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            var replacementsMade = new HashSet<string>();
            var modifiedDocuments = new List<YamlDocument>();

            foreach (var document in documents)
            {
                try
                {
                    var (modifiedDocument, documentChanges) = ProcessDocument(document, imagesToUpdate);
                    modifiedDocuments.Add(modifiedDocument);
                    replacementsMade.UnionWith(documentChanges);
                }
                catch (Exception ex)
                {
                    log.WarnFormat("Skipping corrupted YAML document: {0}", ex.Message);
                    modifiedDocuments.Add(document);
                }
            }

            return (modifiedDocuments, replacementsMade);
        }

        (YamlDocument document, HashSet<string> changes) ProcessDocument(YamlDocument document, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            var changes = new HashSet<string>();

            if (!(document.RootNode is YamlMappingNode rootNode))
            {
                return (document, changes);
            }

            var modifiedRootNode = DeepCopyMappingNode(rootNode);
            var modifiedDocument = new YamlDocument(modifiedRootNode);

            ProcessContainerSpecs(modifiedRootNode, imagesToUpdate, changes);

            return (modifiedDocument, changes);
        }

        void ProcessContainerSpecs(YamlMappingNode node, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> changes)
        {
            var results = new List<ImageReplacementResult>();

            results.Add(ProcessContainersArray(node, FieldNames.Containers, imagesToUpdate));
            results.Add(ProcessContainersArray(node, FieldNames.InitContainers, imagesToUpdate));



            foreach (var kvp in node.Children.ToList())
            {
                if (kvp.Value is YamlMappingNode childMapping)
                {
                    ProcessContainerSpecs(childMapping, imagesToUpdate, changes);
                }
                else if (kvp.Value is YamlSequenceNode sequence)
                {
                    foreach (var item in sequence.OfType<YamlMappingNode>())
                    {
                        ProcessContainerSpecs(item, imagesToUpdate, changes);
                    }
                }
            }

            var combinedResult = ImageReplacementResult.CombineResults(results.ToArray());
            foreach (var replacement in combinedResult.UpdatedImageReferences)
            {
                changes.Add(replacement);
            }
        }

        internal ImageReplacementResult ProcessContainersArray(YamlMappingNode node, string containerKey, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            var results = new List<ImageReplacementResult>();

            foreach (var kvp in node.Children)
            {
                if (kvp.Key is YamlScalarNode scalar && scalar.Value == containerKey && kvp.Value is YamlSequenceNode containersSequence)
                {
                    foreach (var containerNode in containersSequence.OfType<YamlMappingNode>())
                    {
                        var containerResult = ProcessSingleContainer(containerNode, imagesToUpdate);
                        results.Add(containerResult);
                    }
                    break;
                }
            }

            return ImageReplacementResult.CombineResults(results.ToArray());
        }

        private ImageReplacementResult ProcessSingleContainer(YamlMappingNode containerNode, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            foreach (var kvp in containerNode.Children)
            {
                if (kvp.Key is YamlScalarNode scalar && scalar.Value == FieldNames.Image && kvp.Value is YamlScalarNode imageScalar && !string.IsNullOrEmpty(imageScalar.Value))
                {
                    var currentImageRef = ContainerImageReference.FromReferenceString(imageScalar.Value, defaultRegistry);
                    var matchedUpdate = imagesToUpdate
                        .Select(i => new ImageReferenceMatch(i.ContainerReference, i.ContainerReference.CompareWith(currentImageRef)))
                        .FirstOrDefault(i => i.Comparison.MatchesImage());

                    if (matchedUpdate != null && !matchedUpdate.Comparison.TagMatch)
                    {
                        var newImageRef = matchedUpdate.Reference.WithTag(matchedUpdate.Reference.Tag);
                        imageScalar.Value = newImageRef;
                        var replacement = $"{matchedUpdate.Reference.ImageName}:{matchedUpdate.Reference.Tag}";
                        log.Verbose($"Updated container image in strategic merge patch: {newImageRef}");

                        return new ImageReplacementResult(string.Empty, new HashSet<string> { replacement }, new HashSet<string>());
                    }
                    break;
                }
            }

            return new ImageReplacementResult(string.Empty, new HashSet<string>(), new HashSet<string>());
        }

        YamlMappingNode DeepCopyMappingNode(YamlMappingNode original)
        {
            var copy = new YamlMappingNode();
            foreach (var kvp in original.Children)
            {
                var keyCopy = DeepCopyNode(kvp.Key);
                var valueCopy = DeepCopyNode(kvp.Value);
                copy.Children.Add(keyCopy, valueCopy);
            }
            return copy;
        }

        YamlNode DeepCopyNode(YamlNode original)
        {
            return original switch
            {
                YamlScalarNode scalar => new YamlScalarNode(scalar.Value) { Style = scalar.Style },
                YamlSequenceNode sequence => new YamlSequenceNode(sequence.Select(DeepCopyNode)),
                YamlMappingNode mapping => DeepCopyMappingNode(mapping),
                _ => original
            };
        }



        record ImageReferenceMatch(ContainerImageReference Reference, ContainerImageComparison Comparison);
    }
}
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
            this.yamlContent = yamlContent ?? throw new ArgumentNullException(nameof(yamlContent));
            this.defaultRegistry = defaultRegistry ?? throw new ArgumentNullException(nameof(defaultRegistry));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        ImageReplacementResult NoChangeResult => new ImageReplacementResult(yamlContent, new HashSet<string>());

        public ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                log.Warn("Strategic merge patch file content is empty or whitespace only.");
                return NoChangeResult;
            }

            var documents = LoadYamlDocuments();
            if (documents == null || documents.Count == 0) return NoChangeResult;

            var (modifiedDocuments, replacementsMade) = ProcessAllDocuments(documents, imagesToUpdate);

            if (replacementsMade.Count == 0) return NoChangeResult;

            var modifiedYaml = SerializeDocuments(modifiedDocuments);
            return new ImageReplacementResult(modifiedYaml, replacementsMade);
        }

        private List<YamlDocument>? LoadYamlDocuments()
        {
            try
            {
                using var reader = new StringReader(yamlContent);
                var stream = new YamlStream();
                stream.Load(reader);

                if (stream.Documents.Count == 0)
                {
                    log.Warn("Strategic merge patch file contains no YAML documents.");
                    return null;
                }

                return stream.Documents.ToList();
            }
            catch (YamlException ex)
            {
                log.WarnFormat("Invalid YAML in strategic merge patch: {0}", ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                log.WarnFormat("Error loading YAML documents: {0}", ex.Message);
                return null;
            }
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
            ProcessContainersArray(node, FieldNames.Containers, imagesToUpdate, changes);
            ProcessContainersArray(node, FieldNames.InitContainers, imagesToUpdate, changes);

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
        }

        void ProcessContainersArray(YamlMappingNode node, string containerKey, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> changes)
        {
            foreach (var kvp in node.Children)
            {
                if (kvp.Key is YamlScalarNode scalar && scalar.Value == containerKey && kvp.Value is YamlSequenceNode containersSequence)
                {
                    foreach (var containerNode in containersSequence.OfType<YamlMappingNode>())
                    {
                        ProcessSingleContainer(containerNode, imagesToUpdate, changes);
                    }
                    break;
                }
            }
        }

        void ProcessSingleContainer(YamlMappingNode containerNode, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> changes)
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
                        changes.Add($"{matchedUpdate.Reference.ImageName}:{matchedUpdate.Reference.Tag}");
                        log.Verbose($"Updated container image in strategic merge patch: {newImageRef}");
                    }
                    break;
                }
            }
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

        string SerializeDocuments(List<YamlDocument> documents)
        {
            var newLine = yamlContent.DetectLineEnding() ?? "\n";
            var serializedDocs = new List<string>();
            foreach (var doc in documents)
            {
                using var writer = new StringWriter();
                var tempStream = new YamlStream(doc);
                tempStream.Save(writer, false);
                var serialized = writer.ToString();

                serialized = serialized.TrimEnd();
                if (serialized.EndsWith("..."))
                {
                    serialized = serialized.Substring(0, serialized.Length - 3).TrimEnd();
                }

                serializedDocs.Add(serialized);
            }

            return documents.Count == 1
                ? serializedDocs[0]
                : string.Join($"{newLine}---{newLine}", serializedDocs);
        }

        record ImageReferenceMatch(ContainerImageReference Reference, ContainerImageComparison Comparison);
    }
}
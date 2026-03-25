#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD
{
    /// <summary>
    /// Handles updating container image references in JSON 6902 patch operations written in YAML format.
    /// This is for inline patches in kustomization files that use YAML syntax for JSON patch operations.
    /// </summary>
    public class YamlJson6902PatchImageReplacer : IContainerImageReplacer
    {
        private static class FieldNames
        {
            public const string Op = "op";
            public const string Path = "path";
            public const string Value = "value";
            public const string Image = "image";
        }

        private static class OpValues
        {
            public const string Add = "add";
            public const string Replace = "replace";
        }

        readonly string yamlContent;
        readonly string defaultRegistry;
        readonly ILog log;

        public YamlJson6902PatchImageReplacer(string yamlContent, string defaultRegistry, ILog log)
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
                log.Warn("YAML JSON 6902 patch content is empty or whitespace only.");
                return NoChangeResult;
            }

            YamlStream stream;
            try
            {
                using var reader = new StringReader(yamlContent);
                stream = new YamlStream();
                stream.Load(reader);

                if (stream.Documents.Count == 0)
                {
                    log.Warn("YAML JSON 6902 patch content contains no documents.");
                    return NoChangeResult;
                }
            }
            catch (Exception ex)
            {
                log.WarnFormat("Error parsing YAML JSON 6902 patch content: {0}", ex.Message);
                return NoChangeResult;
            }

            var replacementsMade = new HashSet<string>();

            // Process each document in the YAML stream
            foreach (var document in stream.Documents)
            {
                if (document.RootNode is YamlSequenceNode patchSequence)
                {
                    // JSON 6902 patches are arrays of operation objects
                    foreach (var operationNode in patchSequence.Children.OfType<YamlMappingNode>())
                    {
                        var operationReplacements = ProcessPatchOperation(operationNode, imagesToUpdate);
                        foreach (var replacement in operationReplacements)
                        {
                            replacementsMade.Add(replacement);
                        }
                    }
                }
            }

            if (replacementsMade.Count == 0)
            {
                return NoChangeResult;
            }

            using var writer = new StringWriter();
            // JSON 6902 patches are always single documents by design (RFC 6902 defines patches as single JSON arrays).
            // Create a new stream with just the first document to avoid unwanted document separators.
            if (stream.Documents.Count > 0)
            {
                var singleDocStream = new YamlStream(stream.Documents[0]);
                singleDocStream.Save(writer, false);
            }
            var modifiedContent = writer.ToString().TrimEnd();

            return new ImageReplacementResult(modifiedContent, replacementsMade);
        }

        List<string> ProcessPatchOperation(YamlMappingNode operationNode,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            var opValue = operationNode.GetStringValue(FieldNames.Op);
            var pathValue = operationNode.GetStringValue(FieldNames.Path);

            if (string.IsNullOrEmpty(opValue) || string.IsNullOrEmpty(pathValue))
            {
                return new List<string>();
            }

            return opValue switch
            {
                OpValues.Replace => ProcessReplaceOperation(operationNode, pathValue, imagesToUpdate),
                OpValues.Add => ProcessAddOperation(operationNode, pathValue, imagesToUpdate),
                _ => new List<string>()
            };
        }

        List<string> ProcessReplaceOperation(YamlMappingNode operationNode, string path,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            if (IsImagePath(path) && operationNode.Children.TryGetValue(new YamlScalarNode(FieldNames.Value), out var valueNode))
            {
                if (valueNode is YamlScalarNode imageScalar && !string.IsNullOrEmpty(imageScalar.Value))
                {
                    return ProcessImageReference(imageScalar, imagesToUpdate);
                }
            }

            return new List<string>();
        }

        List<string> ProcessAddOperation(YamlMappingNode operationNode, string path,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            if (IsContainersPath(path) && operationNode.Children.TryGetValue(new YamlScalarNode(FieldNames.Value), out var valueNode))
            {
                if (valueNode is YamlSequenceNode containersSequence)
                {
                    return ProcessContainersSequence(containersSequence, imagesToUpdate);
                }
                else if (valueNode is YamlMappingNode singleContainer)
                {
                    return ProcessContainerMapping(singleContainer, imagesToUpdate);
                }
            }

            return new List<string>();
        }

        List<string> ProcessContainersSequence(YamlSequenceNode containersSequence,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            var allReplacements = new List<string>();

            foreach (var containerNode in containersSequence.Children.OfType<YamlMappingNode>())
            {
                var replacements = ProcessContainerMapping(containerNode, imagesToUpdate);
                allReplacements.AddRange(replacements);
            }

            return allReplacements;
        }

        List<string> ProcessContainerMapping(YamlMappingNode containerNode,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            if (containerNode.Children.TryGetValue(new YamlScalarNode(FieldNames.Image), out var imageNode) &&
                imageNode is YamlScalarNode imageScalar)
            {
                return ProcessImageReference(imageScalar, imagesToUpdate);
            }

            return new List<string>();
        }

        List<string> ProcessImageReference(YamlScalarNode imageScalar,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            if (string.IsNullOrEmpty(imageScalar.Value))
                return new List<string>();

            var currentImageRef = ContainerImageReference.FromReferenceString(imageScalar.Value, defaultRegistry);
            var matchedUpdate = imagesToUpdate
                .Select(i => new ImageReferenceMatch(i.ContainerReference, i.ContainerReference.CompareWith(currentImageRef)))
                .FirstOrDefault(i => i.Comparison.MatchesImage());

            if (matchedUpdate != null && !matchedUpdate.Comparison.TagMatch)
            {
                var newImageRef = matchedUpdate.Reference.WithTag(matchedUpdate.Reference.Tag);
                imageScalar.Value = newImageRef;

                if (imageScalar.Style != ScalarStyle.SingleQuoted && imageScalar.Style != ScalarStyle.DoubleQuoted)
                {
                    imageScalar.Style = ScalarStyle.DoubleQuoted;
                }

                var replacement = $"{matchedUpdate.Reference.ImageName}:{matchedUpdate.Reference.Tag}";
                log.Verbose($"Updated container image in YAML JSON 6902 patch: {newImageRef}");
                return new List<string> { replacement };
            }

            return new List<string>();
        }

        static bool IsImagePath(string path)
        {
            return path.Contains("/containers/") && path.EndsWith("/image") ||
                   path.Contains("/initContainers/") && path.EndsWith("/image");
        }

        static bool IsContainersPath(string path)
        {
            return path.EndsWith("/containers") || path.EndsWith("/initContainers") ||
                   path.EndsWith("/containers/-") || path.EndsWith("/initContainers/-");
        }

        record ImageReferenceMatch(ContainerImageReference Reference, ContainerImageComparison Comparison);
    }
}
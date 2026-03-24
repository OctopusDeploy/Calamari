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
            var hasChanges = false;

            // Process each document in the YAML stream
            foreach (var document in stream.Documents)
            {
                if (document.RootNode is YamlSequenceNode patchSequence)
                {
                    // JSON 6902 patches are arrays of operation objects
                    foreach (var operationNode in patchSequence.Children.OfType<YamlMappingNode>())
                    {
                        if (ProcessPatchOperation(operationNode, imagesToUpdate, replacementsMade))
                        {
                            hasChanges = true;
                        }
                    }
                }
            }

            if (!hasChanges)
            {
                return NoChangeResult;
            }

            using var writer = new StringWriter();
            stream.Save(writer, false);
            var modifiedContent = writer.ToString().TrimEnd();

            return new ImageReplacementResult(modifiedContent, replacementsMade);
        }

        bool ProcessPatchOperation(YamlMappingNode operationNode,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            HashSet<string> replacementsMade)
        {
            var opValue = operationNode.GetStringValue(FieldNames.Op);
            var pathValue = operationNode.GetStringValue(FieldNames.Path);

            if (string.IsNullOrEmpty(opValue) || string.IsNullOrEmpty(pathValue))
            {
                return false;
            }

            return opValue switch
            {
                OpValues.Replace => ProcessReplaceOperation(operationNode, pathValue, imagesToUpdate, replacementsMade),
                OpValues.Add => ProcessAddOperation(operationNode, pathValue, imagesToUpdate, replacementsMade),
                _ => false
            };
        }

        bool ProcessReplaceOperation(YamlMappingNode operationNode, string path,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            HashSet<string> replacementsMade)
        {
            if (IsImagePath(path) && operationNode.Children.TryGetValue(new YamlScalarNode(FieldNames.Value), out var valueNode))
            {
                if (valueNode is YamlScalarNode imageScalar && !string.IsNullOrEmpty(imageScalar.Value))
                {
                    return ProcessImageReference(imageScalar, imagesToUpdate, replacementsMade);
                }
            }

            return false;
        }

        bool ProcessAddOperation(YamlMappingNode operationNode, string path,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            HashSet<string> replacementsMade)
        {
            if (IsContainersPath(path) && operationNode.Children.TryGetValue(new YamlScalarNode(FieldNames.Value), out var valueNode))
            {
                if (valueNode is YamlSequenceNode containersSequence)
                {
                    return ProcessContainersSequence(containersSequence, imagesToUpdate, replacementsMade);
                }
                else if (valueNode is YamlMappingNode singleContainer)
                {
                    return ProcessContainerMapping(singleContainer, imagesToUpdate, replacementsMade);
                }
            }

            return false;
        }

        bool ProcessContainersSequence(YamlSequenceNode containersSequence,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            HashSet<string> replacementsMade)
        {
            var hasChanges = false;

            foreach (var containerNode in containersSequence.Children.OfType<YamlMappingNode>())
            {
                if (ProcessContainerMapping(containerNode, imagesToUpdate, replacementsMade))
                {
                    hasChanges = true;
                }
            }

            return hasChanges;
        }

        bool ProcessContainerMapping(YamlMappingNode containerNode,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            HashSet<string> replacementsMade)
        {
            if (containerNode.Children.TryGetValue(new YamlScalarNode(FieldNames.Image), out var imageNode) &&
                imageNode is YamlScalarNode imageScalar)
            {
                return ProcessImageReference(imageScalar, imagesToUpdate, replacementsMade);
            }

            return false;
        }

        bool ProcessImageReference(YamlScalarNode imageScalar,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            HashSet<string> replacementsMade)
        {
            if (string.IsNullOrEmpty(imageScalar.Value))
                return false;

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

                replacementsMade.Add($"{matchedUpdate.Reference.ImageName}:{matchedUpdate.Reference.Tag}");
                log.Verbose($"Updated container image in YAML JSON 6902 patch: {newImageRef}");
                return true;
            }

            return false;
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
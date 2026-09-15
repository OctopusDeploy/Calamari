#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD
{
    /// <summary>
    /// Handles updating container image references in JSON 6902 patch operations written in YAML format.
    /// This is for inline patches in kustomization files that use YAML syntax for JSON patch operations.
    /// </summary>
    public class YamlJson6902PatchImageReplacer : IContainerImageReplacer
    {
        static class FieldNames
        {
            public const string Op = "op";
            public const string Path = "path";
            public const string Value = "value";
            public const string Image = "image";
        }

        static class OpValues
        {
            public const string Add = "add";
            public const string Replace = "replace";
        }

        readonly string yamlContent;
        readonly string defaultRegistry;
        readonly ILog log;

        public YamlJson6902PatchImageReplacer(string yamlContent, string defaultRegistry, ILog log)
        {
            this.yamlContent = yamlContent;
            this.defaultRegistry = defaultRegistry;
            this.log = log;
        }

        ImageReplacementResult NoChangeResult => new ImageReplacementResult(yamlContent, new HashSet<string>(), new HashSet<string>());

        public ImageReplacementResult UpdateImages(IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            var stream = YamlStreamLoader.TryLoad(yamlContent, log, "YAML JSON 6902 patch");
            if (stream == null)
                return NoChangeResult;

            var results = new List<ImageReplacementResult>();
            var edits = new List<YamlScalarEdit>();

            // Process each document in the YAML stream
            foreach (var document in stream.Documents)
            {
                if (document.RootNode is YamlSequenceNode patchSequence)
                {
                    // JSON 6902 patches are arrays of operation objects
                    foreach (var operationNode in patchSequence.Children.OfType<YamlMappingNode>())
                    {
                        var operationResult = ProcessPatchOperation(operationNode, imagesToUpdate, edits);
                        results.Add(operationResult);
                    }
                }
            }

            var combinedResult = ImageReplacementResult.CombineResults(results.ToArray());
            if (combinedResult.UpdatedImageReferences.Count == 0)
            {
                return NoChangeResult;
            }

            var modifiedContent = YamlScalarSplicer.ReplaceValues(yamlContent, edits);

            return new ImageReplacementResult(modifiedContent, combinedResult.UpdatedImageReferences, combinedResult.AlreadyUpToDateImages);
        }

        ImageReplacementResult ProcessPatchOperation(YamlMappingNode operationNode,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            List<YamlScalarEdit> edits)
        {
            var opValue = operationNode.GetStringValue(FieldNames.Op);
            var pathValue = operationNode.GetStringValue(FieldNames.Path);

            if (string.IsNullOrEmpty(opValue) || string.IsNullOrEmpty(pathValue))
            {
                return NoChangeResult;
            }

            return opValue switch
            {
                OpValues.Replace => ProcessReplaceOperation(operationNode, pathValue, imagesToUpdate, edits),
                OpValues.Add => ProcessAddOperation(operationNode, pathValue, imagesToUpdate, edits),
                _ => NoChangeResult
            };
        }

        ImageReplacementResult ProcessReplaceOperation(YamlMappingNode operationNode, string path,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            List<YamlScalarEdit> edits)
        {
            if (IsImagePath(path) && operationNode.Children.TryGetValue(new YamlScalarNode(FieldNames.Value), out var valueNode))
            {
                if (valueNode is YamlScalarNode imageScalar && !string.IsNullOrEmpty(imageScalar.Value))
                {
                    return ProcessImageReference(imageScalar, imagesToUpdate, edits);
                }
            }

            return NoChangeResult;
        }

        ImageReplacementResult ProcessAddOperation(YamlMappingNode operationNode, string path,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            List<YamlScalarEdit> edits)
        {
            if (IsContainersPath(path) && operationNode.Children.TryGetValue(new YamlScalarNode(FieldNames.Value), out var valueNode))
            {
                if (valueNode is YamlSequenceNode containersSequence)
                {
                    return ProcessContainersSequence(containersSequence, imagesToUpdate, edits);
                }
                else if (valueNode is YamlMappingNode singleContainer)
                {
                    return ProcessContainerMapping(singleContainer, imagesToUpdate, edits);
                }
            }

            return NoChangeResult;
        }

        ImageReplacementResult ProcessContainersSequence(YamlSequenceNode containersSequence,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            List<YamlScalarEdit> edits)
        {
            var results = new List<ImageReplacementResult>();

            foreach (var containerNode in containersSequence.Children.OfType<YamlMappingNode>())
            {
                var result = ProcessContainerMapping(containerNode, imagesToUpdate, edits);
                results.Add(result);
            }

            return ImageReplacementResult.CombineResults(results.ToArray());
        }

        ImageReplacementResult ProcessContainerMapping(YamlMappingNode containerNode,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            List<YamlScalarEdit> edits)
        {
            if (containerNode.Children.TryGetValue(new YamlScalarNode(FieldNames.Image), out var imageNode) &&
                imageNode is YamlScalarNode imageScalar)
            {
                return ProcessImageReference(imageScalar, imagesToUpdate, edits);
            }

            return NoChangeResult;
        }

        ImageReplacementResult ProcessImageReference(YamlScalarNode imageScalar,
            IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate,
            List<YamlScalarEdit> edits)
        {
            if (string.IsNullOrEmpty(imageScalar.Value))
                return NoChangeResult;

            var currentImageRef = ContainerImageReference.FromReferenceString(imageScalar.Value, defaultRegistry);
            var matchedUpdate = imagesToUpdate
                .Select(i => new ImageReferenceMatch(i.ContainerReference, i.ContainerReference.CompareWith(currentImageRef)))
                .FirstOrDefault(i => i.Comparison.MatchesImage());

            if (matchedUpdate != null && !matchedUpdate.Comparison.TagMatch)
            {
                if (!YamlScalarSplicer.CanReplaceValue(yamlContent, imageScalar))
                {
                    log.WarnFormat("Cannot safely update the image reference at line {0} (a {1} scalar). Leaving it unchanged.",
                                   imageScalar.Start.Line,
                                   imageScalar.Style);
                    return NoChangeResult;
                }

                var newImageRef = currentImageRef.WithTag(matchedUpdate.Reference.Tag);
                edits.Add(new YamlScalarEdit(imageScalar, newImageRef.FriendlyName()));

                log.Verbose($"Updated container image in YAML JSON 6902 patch: {newImageRef.FriendlyName()}");

                return new ImageReplacementResult(yamlContent, new HashSet<string> { newImageRef.FriendlyName() }, new HashSet<string>());
            }

            return NoChangeResult;
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
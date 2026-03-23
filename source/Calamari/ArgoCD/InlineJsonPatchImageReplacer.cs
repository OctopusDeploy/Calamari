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
    /// <summary>
    /// Handles updating container image references in inline patches within kustomization.yaml files.
    /// The patches field contains an array of patch objects with inline patch operations.
    /// </summary>
    public class InlineJsonPatchImageReplacer : IContainerImageReplacer
    {
        private static class FieldNames
        {
            public const string Patches = "patches";
            public const string Patch = "patch";
            public const string Containers = "containers";
            public const string InitContainers = "initContainers";
            public const string Image = "image";
        }

        readonly string yamlContent;
        readonly string defaultRegistry;
        readonly ILog log;

        public InlineJsonPatchImageReplacer(string yamlContent, string defaultRegistry, ILog log)
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
                log.Warn("Kustomization file content is empty or whitespace only.");
                return NoChangeResult;
            }

            YamlStream stream = new YamlStream();
            YamlMappingNode rootNode;

            try
            {
                using var reader = new StringReader(yamlContent);
                stream = new YamlStream();
                stream.Load(reader);

                if (stream.Documents.Count != 1 || !(stream.Documents[0].RootNode is YamlMappingNode mappingNode))
                {
                    log.Warn("Kustomization file must contain exactly one YAML document with a mapping root node.");
                    return NoChangeResult;
                }

                rootNode = mappingNode;
            }
            catch (Exception ex)
            {
                log.WarnFormat("Error parsing YAML content: {0}", ex.Message);
                return NoChangeResult;
            }

            var patchesSequence = rootNode.GetSequenceNode(FieldNames.Patches);
            if (patchesSequence == null)
            {
                log.Verbose("No 'patches' sequence found in kustomization file.");
                return NoChangeResult;
            }

            var replacementsMade = new HashSet<string>();
            var hasChanges = false;

            foreach (var patchNode in patchesSequence.OfType<YamlMappingNode>())
            {
                var changes = ProcessPatchNode(patchNode, imagesToUpdate);
                replacementsMade.UnionWith(changes);
                if (changes.Count > 0)
                {
                    hasChanges = true;
                }
            }

            if (!hasChanges)
            {
                return NoChangeResult;
            }

            using var writer = new StringWriter();
            stream.Save(writer, false);
            var modifiedYaml = writer.ToString().TrimEnd();

            return new ImageReplacementResult(modifiedYaml, replacementsMade);
        }

        HashSet<string> ProcessPatchNode(YamlMappingNode patchNode, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            var changes = new HashSet<string>();

            var patchContentValue = patchNode.GetStringValue(FieldNames.Patch);
            if (!string.IsNullOrEmpty(patchContentValue))
            {
                foreach (var kvp in patchNode.Children)
                {
                    if (kvp.Key is YamlScalarNode scalar && scalar.Value == FieldNames.Patch && kvp.Value is YamlScalarNode patchContentScalar)
                    {
                        var patchChanges = ProcessInlinePatchContent(patchContentScalar, imagesToUpdate);
                        changes.UnionWith(patchChanges);
                        break;
                    }
                }
            }

            return changes;
        }

        HashSet<string> ProcessInlinePatchContent(YamlScalarNode patchContentNode, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            var changes = new HashSet<string>();

            try
            {
                var patchContent = patchContentNode.Value;
                if (string.IsNullOrEmpty(patchContent))
                    return changes;

                using var reader = new StringReader(patchContent);
                var patchStream = new YamlStream();
                patchStream.Load(reader);

                if (patchStream.Documents.Count > 0 && patchStream.Documents[0].RootNode is YamlMappingNode patchRoot)
                {
                    var patchChanges = ProcessPatchContentRecursive(patchRoot, imagesToUpdate);
                    changes.UnionWith(patchChanges);

                    if (patchChanges.Count > 0)
                    {
                        using var writer = new StringWriter();
                        patchStream.Save(writer, false);
                        var modifiedPatchContent = writer.ToString().TrimEnd();
                        patchContentNode.Value = modifiedPatchContent;
                    }
                }
            }
            catch (Exception ex)
            {
                log.WarnFormat("Error processing inline patch content: {0}", ex.Message);
            }

            return changes;
        }

        HashSet<string> ProcessPatchContentRecursive(YamlMappingNode node, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate)
        {
            var changes = new HashSet<string>();

            ProcessContainersInNode(node, FieldNames.Containers, imagesToUpdate, changes);
            ProcessContainersInNode(node, FieldNames.InitContainers, imagesToUpdate, changes);

            foreach (var kvp in node.Children)
            {
                if (kvp.Key is YamlScalarNode scalar && scalar.Value == FieldNames.Image && kvp.Value is YamlScalarNode imageScalar && !string.IsNullOrEmpty(imageScalar.Value))
                {
                    ProcessImageReference(imageScalar, imagesToUpdate, changes);
                    break;
                }
            }

            foreach (var kvp in node.Children)
            {
                switch (kvp.Value)
                {
                    case YamlMappingNode childMapping:
                        var childChanges = ProcessPatchContentRecursive(childMapping, imagesToUpdate);
                        changes.UnionWith(childChanges);
                        break;
                    case YamlSequenceNode sequence:
                        foreach (var item in sequence.OfType<YamlMappingNode>())
                        {
                            var itemChanges = ProcessPatchContentRecursive(item, imagesToUpdate);
                            changes.UnionWith(itemChanges);
                        }
                        break;
                }
            }

            return changes;
        }

        void ProcessContainersInNode(YamlMappingNode node, string containerKey, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> changes)
        {
            var containersSequence = node.GetSequenceNode(containerKey);
            if (containersSequence != null)
            {
                foreach (var containerNode in containersSequence.OfType<YamlMappingNode>())
                {
                    foreach (var kvp in containerNode.Children)
                    {
                        if (kvp.Key is YamlScalarNode scalar && scalar.Value == FieldNames.Image && kvp.Value is YamlScalarNode imageScalar)
                        {
                            ProcessImageReference(imageScalar, imagesToUpdate, changes);
                            break;
                        }
                    }
                }
            }
        }

        void ProcessImageReference(YamlScalarNode imageScalar, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> changes)
        {
            if (string.IsNullOrEmpty(imageScalar.Value))
                return;

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

                changes.Add($"{matchedUpdate.Reference.ImageName}:{matchedUpdate.Reference.Tag}");
                log.Verbose($"Updated container image in inline patch: {newImageRef}");
            }
        }

        record ImageReferenceMatch(ContainerImageReference Reference, ContainerImageComparison Comparison);
    }
}
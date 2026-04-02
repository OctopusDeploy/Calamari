#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
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
    public class InlineJsonPatchReplacer : IContainerImageReplacer
    {
        static class FieldNames
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
        readonly KustomizeDiscovery discovery;

        public InlineJsonPatchReplacer(string yamlContent, string defaultRegistry, ILog log)
        {
            this.yamlContent = yamlContent;
            this.defaultRegistry = defaultRegistry;
            this.log = log;
            discovery = new KustomizeDiscovery(log);
        }

        ImageReplacementResult NoChangeResult => new(yamlContent, new HashSet<string>(), new HashSet<string>());

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
            foreach (var patchNode in patchesSequence.OfType<YamlMappingNode>())
            {
                var changes = ProcessPatchNode(patchNode, imagesToUpdate);
                replacementsMade.UnionWith(changes);
            }

            if (!replacementsMade.Any())
            {
                return NoChangeResult;
            }

            using var writer = new StringWriter();
            stream.Save(writer, false);
            var modifiedYaml = writer.ToString().TrimEnd();
            return new ImageReplacementResult(modifiedYaml, replacementsMade, new HashSet<string>());
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

                IContainerImageReplacer patchImageReplacer;
                if (discovery.IsJson6902PatchContent(patchContent!))
                {
                    patchImageReplacer = new YamlJson6902PatchImageReplacer(patchContent!, defaultRegistry, log);
                }
                else
                {
                    patchImageReplacer = new ContainerImageReplacer(patchContent!, defaultRegistry);
                }

                var result = patchImageReplacer.UpdateImages(imagesToUpdate);
                changes.UnionWith(result.UpdatedImageReferences);

                if (result.UpdatedImageReferences.Count > 0)
                {
                    patchContentNode.Value = result.UpdatedContents;
                }
            }
            catch (Exception ex)
            {
                log.WarnFormat("Error processing inline patch content: {0}", ex.Message);
            }

            return changes;
        }

    }
}
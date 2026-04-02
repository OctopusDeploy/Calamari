#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD
{
    public enum PatchType
    {
        StrategicMerge,
        Json6902,
        InlineJsonPatch
    }

    public record PatchFileInfo(string FilePath, PatchType Type);

    public class KustomizePatchDiscovery
    {
        private static class FieldNames
        {
            public const string PatchesStrategicMerge = "patchesStrategicMerge";
            public const string PatchesJson6902 = "patchesJson6902";
            public const string Patches = "patches";
            public const string Path = "path";
        }

        private readonly ICalamariFileSystem fileSystem;
        private readonly ILog log;

        public KustomizePatchDiscovery(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public List<PatchFileInfo> DiscoverPatch(string kustomizationFilePath)
        {
            if (string.IsNullOrWhiteSpace(kustomizationFilePath))
                throw new ArgumentException("Kustomization file path cannot be null or empty", nameof(kustomizationFilePath));

            var patchFiles = new List<PatchFileInfo>();

            if (!fileSystem.FileExists(kustomizationFilePath))
                return patchFiles;

            try
            {
                var content = fileSystem.ReadFile(kustomizationFilePath);

                if (!KustomizationValidator.IsKustomizationResource(content))
                {
                    log.WarnFormat("File {0} is not a properly formatted kustomization file", kustomizationFilePath);
                    return patchFiles;
                }

                if (!ContainsPatchFields(content))
                    return patchFiles;

                var kustomizationDir = Path.GetDirectoryName(kustomizationFilePath)!;
                var rootNode = ParseKustomizationYaml(content);

                if (rootNode == null)
                    return patchFiles;

                ProcessStrategicMergePatches(rootNode, kustomizationDir, patchFiles);
                ProcessJson6902Patches(rootNode, kustomizationDir, patchFiles);
                ProcessInlinePatches(rootNode, kustomizationFilePath, patchFiles);
            }
            catch (YamlException ex)
            {
                log.WarnFormat("Invalid YAML in kustomization file {0}: {1}", kustomizationFilePath, ex.Message);
                return patchFiles;
            }
            catch (IOException ex)
            {
                log.WarnFormat("Could not read kustomization file {0}: {1}", kustomizationFilePath, ex.Message);
                return patchFiles;
            }
            catch (Exception ex)
            {
                log.WarnFormat("Unexpected error parsing kustomization file {0}: {1}", kustomizationFilePath, ex.Message);
                return patchFiles;
            }

            return patchFiles;
        }

        private static bool ContainsPatchFields(string content)
        {
            return content.Contains(FieldNames.Patches) ||
                   content.Contains(FieldNames.PatchesStrategicMerge) ||
                   content.Contains(FieldNames.PatchesJson6902);
        }

        private static YamlMappingNode? ParseKustomizationYaml(string content)
        {
            using var reader = new StringReader(content);
            var stream = new YamlStream();
            stream.Load(reader);

            return stream.Documents.Count == 1 && stream.Documents[0].RootNode is YamlMappingNode rootNode
                ? rootNode
                : null;
        }

        private void ProcessStrategicMergePatches(YamlMappingNode rootNode, string kustomizationDir, List<PatchFileInfo> patchFiles)
        {
            var strategicMergeSequence = rootNode.GetSequenceNode(FieldNames.PatchesStrategicMerge);
            if (strategicMergeSequence == null)
                return;

            var strategicMergePatches = strategicMergeSequence
                .OfType<YamlScalarNode>()
                .Where(pathNode => !string.IsNullOrEmpty(pathNode.Value))
                .Select(pathNode => new PatchFileInfo(
                    Path.IsPathRooted(pathNode.Value!) ? pathNode.Value! : Path.Combine(kustomizationDir, pathNode.Value!),
                    PatchType.StrategicMerge));

            patchFiles.AddRange(strategicMergePatches);
        }

        private void ProcessJson6902Patches(YamlMappingNode rootNode, string kustomizationDir, List<PatchFileInfo> patchFiles)
        {
            var json6902Sequence = rootNode.GetSequenceNode(FieldNames.PatchesJson6902);
            if (json6902Sequence == null)
                return;

            var json6902Patches = json6902Sequence
                .OfType<YamlMappingNode>()
                .Select(entryNode => entryNode.GetStringValue(FieldNames.Path))
                .Where(pathValue => !string.IsNullOrEmpty(pathValue))
                .Select(pathValue => new PatchFileInfo(
                    Path.IsPathRooted(pathValue!) ? pathValue! : Path.Combine(kustomizationDir, pathValue!),
                    PatchType.Json6902));

            patchFiles.AddRange(json6902Patches);
        }

        private static void ProcessInlinePatches(YamlMappingNode rootNode, string kustomizationFilePath, List<PatchFileInfo> patchFiles)
        {
            if (rootNode.ContainsKey(FieldNames.Patches))
            {
                patchFiles.Add(new PatchFileInfo(kustomizationFilePath, PatchType.InlineJsonPatch));
            }
        }

        public static bool HasPatchesNode(string content, ILog log)
        {
            var mappingNode = YamlStreamLoader.TryLoadFirstMappingNode(content, log, "inline patches");
            return mappingNode?.Children.ContainsKey(new YamlScalarNode("patches")) ?? false;
        }

        public static bool HasStrategicMergePatchNode(string content, ILog log)
        {
            var mappingNode = YamlStreamLoader.TryLoadFirstMappingNode(content, log, "inline strategic merge patches");
            if (mappingNode == null)
                return false;

            if (mappingNode.Children.TryGetValue(new YamlScalarNode("patchesStrategicMerge"), out var patchesNode))
            {
                if (patchesNode is YamlSequenceNode sequence)
                {
                    // Look for inline YAML patches (multi-line strings starting with |)
                    return sequence.Children.Any(node => node is YamlScalarNode scalar &&
                                                         scalar.Style == ScalarStyle.Literal);
                }
            }

            return false;
        }

        public static bool HasJson6902PatchesNode(string content, ILog log)
        {
            var mappingNode = YamlStreamLoader.TryLoadFirstMappingNode(content, log, "inline JSON 6902 patches");
            if (mappingNode == null)
                return false;

            if (mappingNode.Children.TryGetValue(new YamlScalarNode("patchesJson6902"), out var patchesNode))
            {
                if (patchesNode is YamlSequenceNode sequence)
                {
                    return sequence.Children.OfType<YamlMappingNode>().Any(patchEntry =>
                        patchEntry.Children.TryGetValue(new YamlScalarNode("patch"), out var patchContent) &&
                        patchContent is YamlScalarNode patchScalar &&
                        patchScalar.Style == ScalarStyle.Literal);
                }
            }

            return false;
        }

        public static ImageReplacementResult ProcessInlineStrategicMergePatches(string content, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, string defaultRegistry, ILog log)
        {
            var yamlStream = YamlStreamLoader.TryLoad(content, log, "inline strategic merge patches");
            if (yamlStream?.Documents.Count != 1 || !(yamlStream.Documents[0].RootNode is YamlMappingNode rootNode))
            {
                return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());
            }

            if (!rootNode.Children.TryGetValue(new YamlScalarNode("patchesStrategicMerge"), out var patchesNode) || !(patchesNode is YamlSequenceNode patchSequence))
            {
                return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());
            }

            var allUpdatedImages = new HashSet<string>();
            foreach (var patchNode in patchSequence.Children)
            {
                if (patchNode is YamlScalarNode patchScalar && patchScalar.Style == ScalarStyle.Literal)
                {
                    var patchContent = patchScalar.Value ?? "";
                    var replacer = new ContainerImageReplacer(patchContent, defaultRegistry);
                    var result = replacer.UpdateImages(imagesToUpdate);

                    if (result.UpdatedImageReferences.Count > 0)
                    {
                        patchScalar.Value = result.UpdatedContents;
                        allUpdatedImages.UnionWith(result.UpdatedImageReferences);
                    }
                }
            }

            if (!allUpdatedImages.Any())
            {
                return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());
            }

            using var writer = new StringWriter();
            yamlStream.Save(writer, false);
            var modifiedContent = writer.ToString().TrimEnd();

            return new ImageReplacementResult(modifiedContent, allUpdatedImages, new HashSet<string>());
        }

        public static ImageReplacementResult ProcessInlineJson6902Patches(string content, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, string defaultRegistry, ILog log)
        {
            var yamlStream = YamlStreamLoader.TryLoad(content, log, "inline JSON 6902 patches");
            if (yamlStream?.Documents.Count != 1 || !(yamlStream.Documents[0].RootNode is YamlMappingNode rootNode))
            {
                return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());
            }

            if (!rootNode.Children.TryGetValue(new YamlScalarNode("patchesJson6902"), out var patchesNode) || !(patchesNode is YamlSequenceNode patchSequence))
            {
                return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());
            }

            var allUpdatedImages = new HashSet<string>();

            foreach (var patchEntryNode in patchSequence.Children.OfType<YamlMappingNode>())
            {
                if (patchEntryNode.Children.TryGetValue(new YamlScalarNode("patch"), out var patchContentNode) && patchContentNode is YamlScalarNode patchScalar && patchScalar.Style == ScalarStyle.Literal)
                {
                    var patchContent = patchScalar.Value ?? "";
                    var replacer = new YamlJson6902PatchImageReplacer(patchContent, defaultRegistry, log);
                    var result = replacer.UpdateImages(imagesToUpdate);

                    if (result.UpdatedImageReferences.Count > 0)
                    {
                        patchScalar.Value = result.UpdatedContents;
                        allUpdatedImages.UnionWith(result.UpdatedImageReferences);
                    }
                }
            }

            if (!allUpdatedImages.Any())
            {
                return new ImageReplacementResult(content, new HashSet<string>(), new HashSet<string>());
            }


            using var writer = new StringWriter();
            yamlStream.Save(writer, false);
            var modifiedContent = writer.ToString().TrimEnd();

            return new ImageReplacementResult(modifiedContent, allUpdatedImages, new HashSet<string>());
        }
    }
}
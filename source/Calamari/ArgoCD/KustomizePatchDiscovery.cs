#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public List<PatchFileInfo> DiscoverPatchFiles(string kustomizationFilePath)
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
            if (strategicMergeSequence != null)
            {
                foreach (var pathNode in strategicMergeSequence.OfType<YamlScalarNode>())
                {
                    if (!string.IsNullOrEmpty(pathNode.Value))
                    {
                        var fullPath = Path.IsPathRooted(pathNode.Value) ? pathNode.Value : Path.Combine(kustomizationDir, pathNode.Value);
                        if (fileSystem.FileExists(fullPath))
                        {
                            patchFiles.Add(new PatchFileInfo(fullPath, PatchType.StrategicMerge));
                        }
                        else
                        {
                            log.VerboseFormat("Strategic merge patch file not found: {0}", fullPath);
                        }
                    }
                }
            }
        }

        private void ProcessJson6902Patches(YamlMappingNode rootNode, string kustomizationDir, List<PatchFileInfo> patchFiles)
        {
            var json6902Sequence = rootNode.GetSequenceNode(FieldNames.PatchesJson6902);
            if (json6902Sequence != null)
            {
                foreach (var entryNode in json6902Sequence.OfType<YamlMappingNode>())
                {
                    var pathValue = entryNode.GetStringValue(FieldNames.Path);
                    if (!string.IsNullOrEmpty(pathValue))
                    {
                        var fullPath = Path.IsPathRooted(pathValue) ? pathValue : Path.Combine(kustomizationDir, pathValue);
                        if (fileSystem.FileExists(fullPath))
                        {
                            patchFiles.Add(new PatchFileInfo(fullPath, PatchType.Json6902));
                        }
                        else
                        {
                            log.VerboseFormat("JSON 6902 patch file not found: {0}", fullPath);
                        }
                    }
                }
            }
        }

        private static void ProcessInlinePatches(YamlMappingNode rootNode, string kustomizationFilePath, List<PatchFileInfo> patchFiles)
        {
            if (rootNode.ContainsKey(FieldNames.Patches))
            {
                patchFiles.Add(new PatchFileInfo(kustomizationFilePath, PatchType.InlineJsonPatch));
            }
        }

    }
}
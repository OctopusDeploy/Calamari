#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD
{
    /// <summary>
    /// Helper class for loading and parsing YAML content
    /// </summary>
    public static class YamlStreamLoader
    {
        /// <summary>
        /// Loads YAML content into a YamlStream with error handling and logging.
        /// </summary>
        /// <param name="yamlContent">The YAML content to parse</param>
        /// <param name="log">Logger for warnings and errors</param>
        /// <param name="contextDescription">Description of what is being parsed (for logging)</param>
        /// <returns>YamlStream if successful, null if parsing failed</returns>
        public static YamlStream? TryLoad(string yamlContent, ILog log, string contextDescription)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                log.Warn($"{contextDescription} content is empty or whitespace only.");
                return null;
            }

            try
            {
                using var reader = new StringReader(yamlContent);
                var stream = new YamlStream();
                stream.Load(reader);

                if (stream.Documents.Count == 0)
                {
                    log.Warn($"{contextDescription} content contains no documents.");
                    return null;
                }

                return stream;
            }
            catch (Exception ex)
            {
                log.WarnFormat("Error parsing {0} content: {1}", contextDescription, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Loads YAML content silently (no logging) for validation scenarios.
        /// </summary>
        /// <param name="yamlContent">The YAML content to parse</param>
        /// <returns>YamlStream if successful, null if parsing failed</returns>
        public static YamlStream? TryLoadSilent(string yamlContent)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
                return null;

            try
            {
                using var reader = new StringReader(yamlContent);
                var stream = new YamlStream();
                stream.Load(reader);

                return stream.Documents.Count == 0 ? null : stream;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Loads YAML content and returns the first document's root node if it's a mapping.
        /// </summary>
        /// <param name="yamlContent">The YAML content to parse</param>
        /// <param name="log">Logger for warnings and errors (optional, uses silent loading if null)</param>
        /// <param name="contextDescription">Description of what is being parsed (for logging)</param>
        /// <returns>YamlMappingNode if successful, null if parsing failed or not a mapping</returns>
        public static YamlMappingNode? TryLoadFirstMappingNode(string yamlContent, ILog? log = null, string contextDescription = "YAML")
        {
            var stream = log != null
                ? TryLoad(yamlContent, log, contextDescription)
                : TryLoadSilent(yamlContent);

            if (stream == null || stream.Documents.Count == 0)
                return null;

            return stream.Documents[0].RootNode as YamlMappingNode;
        }

        /// <summary>
        /// Serializes YAML documents back to string format, preserving line endings and handling document separators.
        /// </summary>
        /// <param name="documents">The YAML documents to serialize</param>
        /// <param name="originalContent">Original YAML content to detect line endings from (optional)</param>
        /// <returns>Serialized YAML string</returns>
        public static string SerializeDocuments(IEnumerable<YamlDocument> documents, string? originalContent = null)
        {
            if (documents == null)
                throw new ArgumentNullException(nameof(documents));

            var documentList = documents.ToList();
            if (documentList.Count == 0)
                return string.Empty;

            var newLine = originalContent?.DetectLineEnding() ?? "\n";
            var serializedDocs = documentList.Select(doc => SerializeDocument(doc, newLine));

            var joined = string.Join($"{newLine}---{newLine}", serializedDocs);
            return EndsWithNewLine(originalContent) ? joined + newLine : joined;
        }

        static string SerializeDocument(YamlDocument document, string newLine)
        {
            // The emitter always writes '\n' regardless of the writer's NewLine, so the document's
            // own line ending has to be reapplied afterwards.
            using var writer = new StringWriter();
            new YamlStream(document).Save(writer, false);

            var serialized = writer.ToString().TrimEnd().ReplaceLineEndings(newLine);
            return serialized.EndsWith("...")
                ? serialized.Substring(0, serialized.Length - 3).TrimEnd()
                : serialized;
        }

        static bool EndsWithNewLine(string? content)
        {
            return content != null && (content.EndsWith("\n") || content.EndsWith("\r"));
        }
    }
}
#nullable enable
using System;
using System.IO;
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
    }
}
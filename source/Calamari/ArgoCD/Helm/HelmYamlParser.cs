#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Calamari.ArgoCD.Helm
{
    /// <summary>
    /// Provides a wrapper around YAML content using yamldotnet
    /// </summary>
    public class HelmYamlParser
    {
        readonly char[] whitespaceButNotNewlines = { ' ', '\t', '\f', '\v' };
        public HelmYamlParser(string yamlContent)
        {
            yamlString = yamlContent.Trim(whitespaceButNotNewlines);
            var reader = new StringReader(yamlString);
            yamlStream = new YamlStream();
            yamlStream.Load(reader);
        }

        readonly string yamlString;
        readonly YamlStream yamlStream;

        public string GetValueAtPath(string path)
        {
            var nodeAtPath = GetNodeAtPath(path);
            return nodeAtPath?.Value ?? string.Empty;
        }

        YamlScalarNode? GetNodeAtPath(string path)
        {
            path = path.Trim();

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;
            var pathParts = path.Split('.');

            YamlMappingNode current = root;
            for (var index = 0; index < pathParts.Length - 1; index++)
            {
                var key = new YamlScalarNode(pathParts[index]);
                if (!current.Children.TryGetValue(key, out var nextNode))
                {
                    throw new InvalidOperationException($"Path segment '{pathParts[index]}' not found.");
                }

                current = nextNode as YamlMappingNode
                          ?? throw new InvalidOperationException($"'{pathParts[index]}' is not a mapping node.");
            }

            var lastKey = new YamlScalarNode(pathParts.Last());
            current.Children.TryGetValue(lastKey, out var scalarNode);

            return scalarNode as YamlScalarNode;
        }

        public List<string> CreateDotPathsForNodes()
        {
            var deserializer = new DeserializerBuilder().Build();
            var yamlObject = deserializer.Deserialize(yamlString);

            var paths = new List<string>();
            FlattenObject(yamlObject, "", paths);

            return paths;
        }

        public string UpdateContentForPath(string path, string newValue)
        {
            var nodeAtPath = GetNodeAtPath(path);
            if (nodeAtPath != null)
            {
                return ReplaceNodeContent(nodeAtPath, newValue);
            }

            return yamlString;
        }

        // Splices the new value into the original text rather than rebuilding it line by line, so the
        // file's own line endings, trailing newline and encoding survive untouched. Rebuilding produced
        // whole-file diffs whenever the file's convention differed from the agent's Environment.NewLine.
        string ReplaceNodeContent(YamlScalarNode node, string newValue)
        {
            var (startColumn, endColumn) = ValueColumns(node);
            var start = OffsetOfLine((int)node.Start.Line) + startColumn;
            var end = OffsetOfLine((int)node.End.Line) + endColumn;

            return yamlString[..start] + newValue + yamlString[end..];
        }

        static (int startColumn, int endColumn) ValueColumns(YamlScalarNode node)
        {
            switch (node.Style)
            {
                case ScalarStyle.Literal:
                case ScalarStyle.Plain:
                    return ((int)node.Start.Column - 1, (int)node.End.Column - 1);
                case ScalarStyle.DoubleQuoted:
                case ScalarStyle.SingleQuoted:
                    return ((int)node.Start.Column, (int)node.End.Column - 2);
                default:
                    throw new NotSupportedException("Modifying Folded or Ambiguous Scar Values is not supported.");
            }
        }

        /// <summary>
        /// Index of the first character of the given 1-based line, counting YAML line breaks (\r\n, \n and \r).
        /// </summary>
        int OffsetOfLine(int line)
        {
            var currentLine = 1;
            var index = 0;
            while (currentLine < line && index < yamlString.Length)
            {
                var character = yamlString[index++];
                if (character == '\r' && index < yamlString.Length && yamlString[index] == '\n')
                    index++;
                if (character == '\r' || character == '\n')
                    currentLine++;
            }

            return index;
        }

        static void FlattenObject(object? obj, string currentPath, List<string> paths)
        {
            switch (obj)
            {
                case null:
                {
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        paths.Add(currentPath);
                    }

                    return;
                }
                case Dictionary<object, object> dict:
                {
                    foreach (var kvp in dict)
                    {
                        var key = kvp.Key.ToString() ?? "";
                        // Ignore any keys that are using dot notation. Helm does not support these specifying values.
                        if (!key.Contains('.'))
                        {
                            var newPath = string.IsNullOrEmpty(currentPath) ? key : $"{currentPath}.{key}";
                            FlattenObject(kvp.Value, newPath, paths);
                        }
                    }

                    break;
                }
                case List<object> list:
                {
                /* NOTE: We currently ignore Index style sections
                // E.g.
                    images:
                       - nginx:
                           value: docker.io/nginx
                           version: 2.12
                       - cache:
                           value: redis
                           version: 1.98
                This approach with helm is massively UNCOMMON as it makes it difficult to reference images directly inside charts
                Given this, and the fact it also makes it hard for us to resolve values, we just ignore in favour of Dictionary/Map/Flat objects
                */
                    break;
                }
                default:
                {
                    // Leaf node - add the path
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        paths.Add(currentPath);
                    }
                    break;
                }
            }
        }
    }
}


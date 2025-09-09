#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Helm
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
            endsWithNewline = yamlString.EndsWith(Environment.NewLine);
        }

        readonly string yamlString;
        readonly YamlStream yamlStream;
        readonly bool endsWithNewline;

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

        string ReplaceNodeContent(YamlScalarNode node, string newValue)
        {
            var result = new StringBuilder();
            using var reader = new StringReader(yamlString);

            var targetLine = (int)node.Start.Line;
            int startColumn;
            int endColumn;
            switch (node.Style)
            {
                case ScalarStyle.Literal:
                case ScalarStyle.Plain:
                    startColumn = (int)node.Start.Column - 1;
                    endColumn = (int)node.End.Column - 1;
                    break;
                case ScalarStyle.DoubleQuoted:
                case ScalarStyle.SingleQuoted:
                    startColumn = (int)node.Start.Column;
                    endColumn = (int)node.End.Column - 2;
                    break;
                default:
                    throw new NotSupportedException("Modifying Folded or Ambiguous Scar Values is not supported.");
            }

            int currentLine = 1;

            while (reader.ReadLine() is { } line)
            {
                if (currentLine == targetLine)
                {
                    // Replace in this line
                    var before = line[..startColumn];
                    var after = line[endColumn..];
                    result.AppendLine(before + newValue + after);
                }
                else
                {
                    result.AppendLine(line);
                }

                currentLine++;
            }

            return endsWithNewline ? result.ToString() : result.ToString().TrimEnd();
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
                        var newPath = string.IsNullOrEmpty(currentPath) ? key : $"{currentPath}.{key}";
                        FlattenObject(kvp.Value, newPath, paths);
                    }

                    break;
                }
                case List<object> list:
                {
                    for (var index = 0; index < list.Count; index++)
                    {
                        var newPath = $"{currentPath}[{index}]";
                        FlattenObject(list[index], newPath, paths);
                    }

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

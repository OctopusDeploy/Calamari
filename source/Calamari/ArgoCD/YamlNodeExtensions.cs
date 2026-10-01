#nullable enable
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD
{
    public static class YamlNodeExtensions
    {
        /// <summary>
        /// Gets a sequence node from a mapping node by key name.
        /// </summary>
        public static YamlSequenceNode? GetSequenceNode(this YamlMappingNode node, string key)
        {
            var keyNode = new YamlScalarNode(key);
            foreach (var kvp in node.Children)
            {
                if (keyNode.Equals(kvp.Key) && kvp.Value is YamlSequenceNode sequenceNode)
                {
                    return sequenceNode;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a string value from a mapping node by key name.
        /// </summary>
        public static string? GetStringValue(this YamlMappingNode node, string key)
        {
            var keyNode = new YamlScalarNode(key);
            foreach (var kvp in node.Children)
            {
                if (keyNode.Equals(kvp.Key) && kvp.Value is YamlScalarNode scalarNode)
                {
                    return scalarNode.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a mapping node from a mapping node by key name.
        /// </summary>
        public static YamlMappingNode? GetMappingNode(this YamlMappingNode node, string key)
        {
            var keyNode = new YamlScalarNode(key);
            foreach (var kvp in node.Children)
            {
                if (keyNode.Equals(kvp.Key) && kvp.Value is YamlMappingNode mappingNode)
                {
                    return mappingNode;
                }
            }
            return null;
        }

        /// <summary>
        /// Checks if a mapping node contains a key.
        /// </summary>
        public static bool ContainsKey(this YamlMappingNode node, string key)
        {
            var keyNode = new YamlScalarNode(key);
            return node.Children.Any(kvp => keyNode.Equals(kvp.Key));
        }
    }
}
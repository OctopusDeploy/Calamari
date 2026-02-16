using System;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes.Patching;

public static class JsonNodeExtensionMethods
{
    /// <summary>
    /// Converts a JsonNode to a YamlNode.
    /// </summary>
    /// <param name="jsonNode">The JSON node to convert</param>
    /// <returns>A YamlNode representation of the JSON node</returns>
    public static YamlNode ToYamlNode(this JsonNode? jsonNode)
    {
        if (jsonNode == null)
        {
            return new YamlScalarNode("null");
        }

        return jsonNode switch
        {
            JsonValue value => ConvertJsonValue(value),
            JsonArray array => ConvertJsonArray(array),
            JsonObject obj => ConvertJsonObject(obj),
            _ => throw new NotSupportedException($"Unsupported JSON node type: {jsonNode.GetType().Name}")
        };
    }

    static YamlScalarNode ConvertJsonValue(JsonValue value)
    {
        // Try to get the underlying value
        if (value.TryGetValue<bool>(out var boolValue))
        {
            return new YamlScalarNode(boolValue.ToString().ToLowerInvariant());
        }

        if (value.TryGetValue<long>(out var longValue))
        {
            return new YamlScalarNode(longValue.ToString());
        }

        if (value.TryGetValue<double>(out var doubleValue))
        {
            return new YamlScalarNode(doubleValue.ToString("G"));
        }

        if (value.TryGetValue<string>(out var stringValue))
        {
            // Quote strings to avoid YAML ambiguity
            return new YamlScalarNode(stringValue ?? "null") { Style = ScalarStyle.DoubleQuoted };
        }

        // Fallback to string representation
        return new YamlScalarNode(value.ToJsonString());
    }

    static YamlSequenceNode ConvertJsonArray(JsonArray array)
    {
        var yamlSequence = new YamlSequenceNode();
        foreach (var item in array)
        {
            yamlSequence.Add(item.ToYamlNode());
        }
        return yamlSequence;
    }

    static YamlMappingNode ConvertJsonObject(JsonObject obj)
    {
        var yamlMapping = new YamlMappingNode();
        foreach (var property in obj)
        {
            var key = new YamlScalarNode(property.Key);
            var value = property.Value.ToYamlNode();
            yamlMapping.Add(key, value);
        }
        return yamlMapping;
    }
}

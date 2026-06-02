using System;
using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes.Patching;

public static class YamlDocumentExtensionMethods
{
    /// <summary>
    /// Converts a YamlDocument to a JsonNode.
    /// </summary>
    /// <param name="yamlDocument">The YAML document to convert</param>
    /// <returns>A JsonNode representation of the YAML document, or null if the document is empty</returns>
    public static JsonNode? ToJsonNode(this YamlDocument yamlDocument)
    {
        if (yamlDocument.RootNode == null)
        {
            return null;
        }

        return ConvertYamlNodeToJsonNode(yamlDocument.RootNode);
    }

    static JsonNode? ConvertYamlNodeToJsonNode(YamlNode yamlNode)
    {
        return yamlNode switch
        {
            YamlScalarNode scalar => ConvertScalar(scalar),
            YamlSequenceNode sequence => ConvertSequence(sequence),
            YamlMappingNode mapping => ConvertMapping(mapping),
            _ => throw new NotSupportedException($"Unsupported YAML node type: {yamlNode.GetType().Name}")
        };
    }

    static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        // Handle null values
        if (scalar.Value == null || scalar.Value == "null" || scalar.Value == "~")
        {
            return null;
        }

        // Try to parse as boolean
        if (bool.TryParse(scalar.Value, out var boolValue))
        {
            return JsonValue.Create(boolValue);
        }

        // Try to parse as integer
        if (long.TryParse(scalar.Value, out var longValue))
        {
            return JsonValue.Create(longValue);
        }

        // Try to parse as double
        if (double.TryParse(scalar.Value, out var doubleValue))
        {
            return JsonValue.Create(doubleValue);
        }

        // Default to string
        return JsonValue.Create(scalar.Value);
    }

    static JsonArray ConvertSequence(YamlSequenceNode sequence)
    {
        var jsonArray = new JsonArray();
        foreach (var item in sequence.Children)
        {
            var jsonNode = ConvertYamlNodeToJsonNode(item);
            jsonArray.Add(jsonNode);
        }
        return jsonArray;
    }

    static JsonObject ConvertMapping(YamlMappingNode mapping)
    {
        var jsonObject = new JsonObject();
        foreach (var entry in mapping.Children)
        {
            if (entry.Key is not YamlScalarNode scalarKey)
            {
                throw new NotSupportedException("Only scalar keys are supported in YAML mappings");
            }

            var key = scalarKey.Value ?? string.Empty;
            var value = ConvertYamlNodeToJsonNode(entry.Value);
            jsonObject[key] = value;
        }
        return jsonObject;
    }
}

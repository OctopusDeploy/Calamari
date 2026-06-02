using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Calamari.Kubernetes.Patching.JsonPatch;

/// <summary>
/// Generates JSON Patch documents by comparing two JSON documents according to RFC 6902.
/// The generator creates a patch that, when applied to the original document, produces the modified document.
/// 
/// This implementation is a straightforward recursive comparison that produces a minimal set of operations
/// (add, remove, replace) without attempting to detect moves or copies.
/// We expect to add support for the additional operations when the need arises.
/// </summary>
public static class JsonPatchGenerator
{
    /// <summary>
    /// Generates a JSON Patch document that describes the differences between two JSON documents.
    /// </summary>
    /// <param name="original">The original JSON document</param>
    /// <param name="modified">The modified JSON document</param>
    /// <returns>A JsonPatchDocument containing the operations needed to transform original into modified</returns>
    public static JsonPatchDocument Generate(JsonNode? original, JsonNode? modified)
    {
        var operations = new List<JsonPatchOperation>();
        CompareNodes(new JsonPointer(""), original, modified, operations);
        return new JsonPatchDocument(operations);
    }

    static void CompareNodes(JsonPointer path, JsonNode? original, JsonNode? modified, List<JsonPatchOperation> operations)
    {
        if (original == null && modified == null)
        {
            return;
        }

        // Original exists, modified null - remove
        if (original != null && modified == null)
        {
            if (!path.IsEmpty)
            {
                operations.Add(JsonPatchOperation.Remove(path));
            }
            return;
        }

        // Original null, modified exists - add
        if (original == null && modified != null)
        {
            operations.Add(
                path.IsEmpty
                    ? JsonPatchOperation.Replace(path, modified.DeepClone())
                    : JsonPatchOperation.Add(path, modified.DeepClone())
            );
            return;
        }

        // Both exist - check types and recurse
        var originalKind = GetNodeKind(original!);
        var modifiedKind = GetNodeKind(modified!);

        // Different types - replace
        if (originalKind != modifiedKind)
        {
            operations.Add(JsonPatchOperation.Replace(path, modified!.DeepClone()));
            return;
        }

        // Same type - recurse by type
        switch (originalKind)
        {
            case JsonNodeKind.Object:
                CompareObjects(path, (JsonObject)original!, (JsonObject)modified!, operations);
                break;
            case JsonNodeKind.Array:
                CompareArrays(path, (JsonArray)original!, (JsonArray)modified!, operations);
                break;
            case JsonNodeKind.Value:
                CompareValues(path, original!, modified!, operations);
                break;
        }
    }

    static void CompareObjects(JsonPointer path, JsonObject original, JsonObject modified, List<JsonPatchOperation> operations)
    {
        // Check for removed and modified properties
        foreach (var prop in original)
        {
            var propertyPath = AppendToPath(path, prop.Key);

            if (!modified.ContainsKey(prop.Key))
            {
                // Property removed
                operations.Add(JsonPatchOperation.Remove(propertyPath));
            }
            else
            {
                // Property exists in both
                var originalValue = prop.Value;
                var modifiedValue = modified[prop.Key];

                // Special case: if modified value is null (JSON null), handle it as a replace
                // because JsonObject[key] returns C# null for JSON null values
                if (modifiedValue == null && originalValue != null)
                {
                    operations.Add(JsonPatchOperation.Replace(propertyPath, (JsonNode?)null));
                }
                else
                {
                    // Both non-null or both null - compare normally
                    CompareNodes(propertyPath, originalValue, modifiedValue, operations);
                }
            }
        }

        // Check for added properties
        foreach (var prop in modified)
        {
            if (!original.ContainsKey(prop.Key))
            {
                var propertyPath = AppendToPath(path, prop.Key);
                operations.Add(JsonPatchOperation.Add(propertyPath, prop.Value?.DeepClone()));
            }
        }
    }

    static void CompareArrays(JsonPointer path, JsonArray original, JsonArray modified, List<JsonPatchOperation> operations)
    {
        var minLength = Math.Min(original.Count, modified.Count);

        // Compare overlapping elements
        for (var i = 0; i < minLength; i++)
        {
            var elementPath = AppendToPath(path, i.ToString());

            // Special case: if modified value is null (JSON null), handle it as a replace
            // because JsonArray[i] returns C# null for JSON null values
            if (modified[i] == null && original[i] != null)
            {
                operations.Add(JsonPatchOperation.Replace(elementPath, (JsonNode?)null));
            }
            else
            {
                CompareNodes(elementPath, original[i], modified[i], operations);
            }
        }

        // Handle length differences
        if (original.Count > modified.Count)
        {
            // Elements removed - remove from highest index to lowest to avoid invalidation
            for (var i = original.Count - 1; i >= modified.Count; i--)
            {
                var elementPath = AppendToPath(path, i.ToString());
                operations.Add(JsonPatchOperation.Remove(elementPath));
            }
        }
        else if (modified.Count > original.Count)
        {
            // Elements added - use "/-" syntax for appending
            foreach (var element in modified.Skip(original.Count))
            {
                var elementPath = AppendToPath(path, "-");
                operations.Add(JsonPatchOperation.Add(elementPath, element?.DeepClone()));
            }
        }
    }

    static void CompareValues(JsonPointer path, JsonNode original, JsonNode modified, List<JsonPatchOperation> operations)
    {
        if (!JsonPatchDocumentExtensionMethods.JsonNodesEqual(original, modified))
        {
            operations.Add(JsonPatchOperation.Replace(path, modified.DeepClone()));
        }
    }

    static JsonNodeKind GetNodeKind(JsonNode node)
    {
        return node switch
        {
            JsonObject => JsonNodeKind.Object,
            JsonArray => JsonNodeKind.Array,
            _ => JsonNodeKind.Value,
        };
    }

    static JsonPointer AppendToPath(JsonPointer basePath, string token)
    {
        var newTokens = new string[basePath.Tokens.Length + 1];
        Array.Copy(basePath.Tokens, newTokens, basePath.Tokens.Length);
        newTokens[^1] = token;
        return new JsonPointer(newTokens);
    }
}

enum JsonNodeKind
{
    Object,
    Array,
    Value,
}

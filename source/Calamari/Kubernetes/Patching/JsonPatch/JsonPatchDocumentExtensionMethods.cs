using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Calamari.Kubernetes.Patching.JsonPatch;

public static class JsonPatchDocumentExtensionMethods
{
    /// <summary>
    /// Applies a JSON Patch document to a source JSON node according to RFC 6902.
    /// Operations are applied in sequence. If any operation fails, a JsonPatchApplicationException is thrown.
    /// </summary>
    /// <exception cref="JsonPatchApplicationException">
    /// Thrown if the patch cannot be applied, for example if it points to some nodes that do not exist within the source document
    /// </exception>
    /// <param name="patchDocument">The JSON Patch document containing operations to apply</param>
    /// <param name="source">The source JSON node to patch</param>
    /// <returns>A new JsonNode with the patch applied</returns>
    public static JsonNode Apply(this JsonPatchDocument patchDocument, JsonNode source)
    {
        // Clone the source to avoid mutating the original
        var result = source.DeepClone();

        foreach (var operation in patchDocument.Operations)
        {
            try
            {
                result = operation.Op switch
                {
                    OperationType.Add => ApplyAdd(result, operation),
                    OperationType.Remove => ApplyRemove(result, operation),
                    OperationType.Replace => ApplyReplace(result, operation),
                    OperationType.Move => ApplyMove(result, operation),
                    OperationType.Copy => ApplyCopy(result, operation),
                    OperationType.Test => ApplyTest(result, operation),
                    _ => throw new JsonPatchApplicationException($"Unknown operation type: {operation.Op}")
                };
            }
            catch (Exception ex) when (ex is not JsonPatchApplicationException)
            {
                throw new JsonPatchApplicationException($"Failed to apply {operation.Op} operation at path '{operation.Path}'", ex);
            }
        }

        return result;
    }

    static JsonNode ApplyAdd(JsonNode root, JsonPatchOperation operation)
    {
        if (!operation.HasValueProperty) throw new JsonPatchApplicationException("'add' operation requires missing 'value' property");

        if (operation.Path.IsEmpty)
        {
            // Adding to root replaces the entire document
            return operation.Value ?? throw new JsonPatchApplicationException("Cannot add null value at root");
        }

        var parentPath = operation.Path.GetParent(out var lastToken);
        var parent = root.NavigateToNode(parentPath);

        if (parent is JsonObject obj)
        {
            obj[lastToken] = operation.Value?.DeepClone();
        }
        else if (parent is JsonArray arr)
        {
            var index = JsonPointerExtensionMethods.ParseArrayIndex(lastToken, arr, allowAppend: true);
            arr.Insert(index, operation.Value?.DeepClone());
        }
        else
        {
            throw new JsonPatchApplicationException($"Cannot add to parent of type {parent?.GetType().Name}");
        }

        return root;
    }

    static JsonNode ApplyRemove(JsonNode root, JsonPatchOperation operation)
    {
        if (operation.Path.IsEmpty) throw new JsonPatchApplicationException("Cannot remove root element");
        
        var parentPath = operation.Path.GetParent(out var lastToken);
        var parent = root.NavigateToNode(parentPath);

        if (parent is JsonObject obj)
        {
            if (!obj.ContainsKey(lastToken))
            {
                throw new JsonPatchApplicationException($"Path not found: {operation.Path}");
            }

            obj.Remove(lastToken);
        }
        else if (parent is JsonArray arr)
        {
            var index = JsonPointerExtensionMethods.ParseArrayIndex(lastToken, arr, allowAppend: false);
            arr.RemoveAt(index);
        }
        else
        {
            throw new JsonPatchApplicationException($"Cannot remove from parent of type {parent?.GetType().Name}");
        }

        return root;
    }

    static JsonNode ApplyReplace(JsonNode root, JsonPatchOperation operation)
    {
        if (!operation.HasValueProperty) throw new JsonPatchApplicationException("'replace' operation requires missing 'value' property");

        if (operation.Path.IsEmpty)
        {
            // Replacing root
            return operation.Value ?? throw new JsonPatchApplicationException("Cannot replace root with null");
        }

        var parentPath = operation.Path.GetParent(out var lastToken);
        var parent = root.NavigateToNode(parentPath);
        
        if (parent is JsonObject obj)
        {
            if (!obj.ContainsKey(lastToken))
            {
                throw new JsonPatchApplicationException($"Path not found: {operation.Path}");
            }

            obj[lastToken] = operation.Value?.DeepClone();
        }
        else if (parent is JsonArray arr)
        {
            var index = JsonPointerExtensionMethods.ParseArrayIndex(lastToken, arr, allowAppend: false);
            arr[index] = operation.Value?.DeepClone();
        }
        else
        {
            throw new JsonPatchApplicationException($"Cannot replace in parent of type {parent?.GetType().Name}");
        }

        return root;
    }

    static JsonNode ApplyMove(JsonNode root, JsonPatchOperation operation)
    {
        if (operation.From == null) throw new JsonPatchApplicationException("Move operation requires 'from' field");

        var fromPath = operation.From.Value;
        var toPath = operation.Path;

        // If moving to the same location, it's a no-op
        if (toPath == fromPath) return root;

        if (toPath.IsDescendantOf(fromPath)) throw new JsonPatchApplicationException("Cannot move a value to one of its own children");

        // Get the value at 'from'. Don't need to clone because Add will do it.
        var value = root.NavigateToNode(fromPath);
        
        root = ApplyRemove(root, new JsonPatchOperation { Op = OperationType.Remove, Path = operation.From.Value });
        root = ApplyAdd(root, new JsonPatchOperation { Op = OperationType.Add, Path = operation.Path, Value = value });

        return root;
    }

    static JsonNode ApplyCopy(JsonNode root, JsonPatchOperation operation)
    {
        if (operation.From is not { } fromPath) throw new JsonPatchApplicationException("Copy operation requires 'from' field");

        // Get the value at 'from'. Don't need to clone because Add will do it.
        var value = root.NavigateToNode(fromPath);

        root = ApplyAdd(root, new JsonPatchOperation { Op = OperationType.Add, Path = operation.Path, Value = value });

        return root;
    }

    static JsonNode ApplyTest(JsonNode root, JsonPatchOperation operation)
    {
        if (!operation.HasValueProperty) throw new JsonPatchApplicationException("'test' operation requires missing 'value' property");

        var expectedValue = operation.Value;
        var actualValue = root.NavigateToNode(operation.Path);

        if (!JsonNodesEqual(expectedValue, actualValue)) throw new JsonPatchApplicationException($"Test operation failed at path '{operation.Path}'. Values do not match.");

        return root;
    }

    // This is expensive, requiring string-serialization roundtrips, but it's
    // only used for the `Test` operation which is rare and unlikely in production.
    internal static bool JsonNodesEqual(JsonNode? a, JsonNode? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        // Use JsonElement for semantic comparison (ignores property order)
        using var docA = JsonDocument.Parse(a.ToJsonString());
        using var docB = JsonDocument.Parse(b.ToJsonString());
        return JsonElementsEqual(docA.RootElement, docB.RootElement);
    }

    static bool JsonElementsEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;

        switch (a.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return true;

            case JsonValueKind.Number:
                return a.GetRawText() == b.GetRawText();

            case JsonValueKind.String:
                return a.GetString() == b.GetString();

            case JsonValueKind.Array:
                if (a.GetArrayLength() != b.GetArrayLength()) return false;
                using (var enumA = a.EnumerateArray())
                using (var enumB = b.EnumerateArray())
                {
                    while (enumA.MoveNext() && enumB.MoveNext())
                    {
                        if (!JsonElementsEqual(enumA.Current, enumB.Current))
                            return false;
                    }
                }

                return true;

            case JsonValueKind.Object:
                var propsA = a.EnumerateObject().ToList();
                var propsB = b.EnumerateObject().ToList();

                if (propsA.Count != propsB.Count) return false;

                // Create a dictionary for B to handle unordered comparison
                var dictB = propsB.ToDictionary(p => p.Name, p => p.Value);

                foreach (var propA in propsA)
                {
                    if (!dictB.TryGetValue(propA.Name, out var valueB)) return false;
                    if (!JsonElementsEqual(propA.Value, valueB)) return false;
                }

                return true;

            default:
                return false;
        }
    }
}

public class JsonPatchApplicationException : Exception
{
    public JsonPatchApplicationException(string? message) : base(message)
    {
    }

    public JsonPatchApplicationException(string message, Exception inner) : base(message, inner)
    {
    }
}

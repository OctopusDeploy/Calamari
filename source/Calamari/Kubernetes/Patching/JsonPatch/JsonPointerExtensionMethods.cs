using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Calamari.Kubernetes.Patching.JsonPatch;

public static class JsonPointerExtensionMethods
{
    public static JsonNode? NavigateToNode(this JsonNode current, JsonPointer pointer)
    {
        var tokens = pointer.Tokens;
        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            var isLastToken = i == tokens.Length - 1;

            if (current is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(token, out var next))
                {
                    throw new JsonException($"Path not found: property '{token}' does not exist");
                }

                // We can navigate to a null/missing entry at the end of the path (i.e. we looked for container.person.name and they didn't have a name)
                // but we cannot navigate through one in the middle of the chain (i.e. we want to go container.person.name and there's no person)
                if (next == null)
                {
                    if (isLastToken) return null;

                    throw new JsonException($"Attempt to navigate to {pointer}, but found null at property '{token}'");
                }

                current = next;
            }
            else if (current is JsonArray arr)
            {
                var index = ParseArrayIndex(token, arr, allowAppend: false);
                var element = arr[index];
                
                if (element == null)
                {
                    if (isLastToken) return null;

                    throw new JsonException($"Attempt to navigate to {pointer}, but found null at array index '{index}'");
                }

                current = element;
            }
            else
            {
                throw new JsonException($"Cannot navigate into {current.GetType().Name}");
            }
        }

        return current;
    }
        
    /// <summary>
    /// Returns true if the given pointer is a descendant of the potential ancestor.
    ///
    /// Examples:
    ///   /foo/bar is a descendant of /foo
    ///   /items/0/name is a descendant of /items
    ///   /foo is not descendant of /foo/bar
    ///   /dog is not descendant of /cat
    ///   /foo is a not descendant of /foo (itself) 
    /// </summary>
    public static bool IsDescendantOf(this JsonPointer candidate, JsonPointer potentialAncestor)
    {
        // Rule: The ancestor must be of lesser (not equal) length
        if (potentialAncestor.Tokens.Length >= candidate.Tokens.Length) return false;

        var ancestorEnumerator = potentialAncestor.Tokens.AsSpan().GetEnumerator();
        var candidateEnumerator = candidate.Tokens.AsSpan().GetEnumerator();

        // Rule: All tokens must match up to the potential ancestor length
        while (ancestorEnumerator.MoveNext() && candidateEnumerator.MoveNext())
        {
            if (!ancestorEnumerator.Current.Equals(candidateEnumerator.Current)) return false;
        }

        return true;
    }

    public static JsonPointer GetParent(this JsonPointer pointer, out string lastToken)
    {
        if (pointer.IsEmpty) throw new JsonException("Invalid path: Cannot GetParent on an empty pointer");

        lastToken = pointer.Tokens[^1];

        return new JsonPointer(pointer.Tokens[0..^1]);
    }

    public static int ParseArrayIndex(string token, JsonArray array, bool allowAppend)
    {
        if (token == "-")
        {
            if (!allowAppend)
            {
                throw new JsonException("Cannot use '-' (append) for this operation");
            }

            return array.Count;
        }

        // RFC 6901: array indices must not have leading zeros (except "0" itself)
        if (token.Length > 1 && token[0] == '0')
        {
            throw new JsonException($"Invalid array index: '{token}' has leading zeros");
        }

        if (!int.TryParse(token, out var index) || index < 0)
        {
            throw new JsonException($"Invalid array index: '{token}'");
        }

        var maxIndex = allowAppend ? array.Count : array.Count - 1;
        if (index > maxIndex)
        {
            throw new JsonException($"Array index {index} out of bounds (array length: {array.Count})");
        }

        return index;
    }
}

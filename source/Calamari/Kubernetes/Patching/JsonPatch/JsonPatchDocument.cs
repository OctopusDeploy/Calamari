using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.Kubernetes.Patching.JsonPatch;

/// <summary>
/// Represents a JSON Patch document. Per the spec the document root is an array of `Operation`
/// Reference: https://jsonpatch.com/
/// </summary>
[JsonConverter(typeof(JsonPatchDocumentConverter))]
public class JsonPatchDocument
{
    public JsonPatchDocument()
    {
        
    }

    public JsonPatchDocument(IEnumerable<JsonPatchOperation> operations)
    {
        Operations = operations.ToList();
    }
    
    public List<JsonPatchOperation> Operations { get; init; } = [];
}
    
public class JsonPatchDocumentConverter : JsonConverter<JsonPatchDocument>
{
    public override JsonPatchDocument? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var operationsList = JsonSerializer.Deserialize<List<JsonPatchOperation>>(ref reader, options);
        if (operationsList == null) return null;
        return new JsonPatchDocument { Operations = operationsList };
    }

    public override void Write(Utf8JsonWriter writer, JsonPatchDocument value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Operations, options);
    }
}


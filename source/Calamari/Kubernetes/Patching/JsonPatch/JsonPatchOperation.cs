using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Calamari.Kubernetes.Patching.JsonPatch;

/// <summary>
/// Reference: https://jsonpatch.com/
/// </summary>
[JsonConverter(typeof(JsonPatchOperationConverter))]
public class JsonPatchOperation
{
    JsonNode? value = null;

    required public OperationType Op { get; set; }
    required public JsonPointer Path { get; set; }

    // For Add and Replace operations
    public JsonNode? Value
    {
        get => value;
        set
        {
            this.value = value;
            HasValueProperty = true;
        }
    }

    // For Copy and Move operations
    public JsonPointer? From { get; set; }

    // Tracks whether the "value" property was present in the JSON
    [JsonIgnore]
    public bool HasValueProperty { get; set; }

    public static JsonPatchOperation Add(JsonPointer path, JsonNode? value) =>
        new()
        {
            Op = OperationType.Add,
            Path = path,
            Value = value,
        };

    public static JsonPatchOperation Remove(JsonPointer path) =>
        new()
        {
            Op = OperationType.Remove,
            Path = path,
        };

    public static JsonPatchOperation Replace(JsonPointer path, JsonNode? value) =>
        new()
        {
            Op = OperationType.Replace,
            Path = path,
            Value = value,
        };
}

/// <summary>
/// The patch operations supported by JSON Patch are "add", "remove", "replace", "move", "copy" and "test". The operations are applied in order: if any of them fail then the whole patch operation should abort.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationType
{
    Add,
    Remove,
    Replace,
    Move,
    Copy,
    Test
}

/// <summary>
/// Custom converter which tracks whether the `value` property was present in the Json or not, so we can distinguish `value: null` from a missing value.
/// </summary>
public class JsonPatchOperationConverter : JsonConverter<JsonPatchOperation>
{
    public override JsonPatchOperation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected start of object");

        OperationType? op = null;
        JsonPointer? path = null;
        JsonNode? value = null;
        JsonPointer? from = null;
        bool hasValueProperty = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (op == null) throw new JsonException("Missing required property 'op'");
                if (path == null) throw new JsonException("Missing required property 'path'");

                return new JsonPatchOperation
                {
                    Op = op.Value,
                    Path = path.Value, // Already null-checked above
                    Value = value,
                    From = from,
                    HasValueProperty = hasValueProperty
                };
            }

            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected property name");

            var propertyName = reader.GetString();
            reader.Read(); // Move to the value

            switch (propertyName)
            {
                case "op" when reader.GetString() is { } opString:
                    op = Enum.Parse<OperationType>(opString, ignoreCase: true);
                    break;

                case "path" when reader.GetString() is { } pathString:
                    path = new JsonPointer(pathString);
                    break;

                case "value":
                    hasValueProperty = true;
                    value = JsonNode.Parse(ref reader);
                    break;

                case "from" when reader.GetString() is { } fromString:
                    from = new JsonPointer(fromString);
                    break;

                default:
                    // Skip unknown properties
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, JsonPatchOperation value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("op", value.Op.ToString().ToLowerInvariant());
        writer.WriteString("path", value.Path.ToString());

        if (value.HasValueProperty)
        {
            writer.WritePropertyName("value");
            if (value.Value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                value.Value.WriteTo(writer);
            }
        }

        if (value.From != null)
        {
            writer.WriteString("from", value.From.Value.ToString());
        }

        writer.WriteEndObject();
    }
}

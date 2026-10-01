using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.Kubernetes.Patching.JsonPatch;

/// <summary>
/// Strongly-typed holder of a string. The string should be in JSON Pointer syntax
/// https://datatracker.ietf.org/doc/html/rfc6901/
/// Note: The string is not validated upon construction and is assumed to be valid until it is actually evaluated in the context of a document.
/// </summary>
[JsonConverter(typeof(JsonPointerConverter))]
public readonly struct JsonPointer : IEquatable<JsonPointer>
{
    public JsonPointer(string value)
    {
        Tokens = value != null ? ParseTokens(value) : throw new ArgumentNullException(nameof(value));
    }

    public JsonPointer(string[] tokens)
    {
        Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public string[] Tokens { get; }

    public bool Equals(JsonPointer other) => Tokens.SequenceEqual(other.Tokens);

    public override bool Equals(object? obj) => obj is JsonPointer other && Equals(other);

    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (var token in Tokens) h.Add(token);
        return h.ToHashCode();
    }

    public static bool operator ==(JsonPointer left, JsonPointer right) => left.Equals(right);

    public static bool operator !=(JsonPointer left, JsonPointer right) => !(left == right);

    /// <summary>
    /// Returns true if the JsonPointer contains the empty string.
    /// </summary>
    public bool IsEmpty => Tokens.Length == 0;

    static string[] ParseTokens(string s)
    {
        if (s is "") return [];

        if (!s.StartsWith('/')) throw new JsonException($"Invalid JSON Pointer: must start with '/' but was '{s}'");

        var tokens = s[1..].Split('/');
        var result = new string[tokens.Length];

        for (var i = 0; i < tokens.Length; i++)
        {
            result[i] = Unescape(tokens[i]);
        }

        return result;
    }

    public override string ToString() => IsEmpty ? "" : "/" + string.Join('/', Tokens.Select(Escape));

    // Unescape: ~1 becomes /, ~0 becomes ~
    static string Escape(string token) => token.Replace("~", "~0").Replace("/", "~1");

    // Unescape: ~1 becomes /, ~0 becomes ~
    static string Unescape(string token) => token.Replace("~1", "/").Replace("~0", "~");
}

public class JsonPointerConverter : JsonConverter<JsonPointer>
{
    public override JsonPointer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var stringValue = JsonSerializer.Deserialize<string>(ref reader, options);
        if (stringValue == null) return default;
        return new JsonPointer(stringValue);
    }

    public override void Write(Utf8JsonWriter writer, JsonPointer value, JsonSerializerOptions options)
    {
        throw new NotImplementedException("We only need to read Json Pointers at this point, not write them");
    }
}

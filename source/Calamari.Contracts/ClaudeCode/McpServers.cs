using System.Text.Json;
using System.Text.Json.Serialization;

namespace Octopus.Calamari.Contracts.ClaudeCode;

public class McpServers(McpServer[] Servers)
{
    public required McpServer[] Servers { get; init; } = Servers;
}

[JsonConverter(typeof(McpServerJsonConverter))]
public abstract class McpServer(string name, IReadOnlyDictionary<string, string> env, IReadOnlyCollection<string> allowedTools)
{
    public required string Name { get; init; } = name;
    public virtual string Type { get; init; }
    public required IReadOnlyDictionary<string, string> Env { get; init; } = env;
    public required IReadOnlyCollection<string> AllowedTools { get; init; } = allowedTools;
}

public class StdioMcpServer(
    string name,
    IReadOnlyDictionary<string, string> env,
    IReadOnlyCollection<string> allowedTools,
    string command,
    IReadOnlyList<string> args) : McpServer(name, env, allowedTools)
{
    public const string DiscriminatorValue = "stdio";
    public override string Type => DiscriminatorValue;

    public required string Command { get; init; } = command;
    public required IReadOnlyList<string> Args { get; init; } = args;
}

public class HttpMcpServer(
    string name,
    IReadOnlyDictionary<string, string> env,
    IReadOnlyCollection<string> allowedTools,
    string url,
    IReadOnlyDictionary<string, string> headers) : McpServer(name, env, allowedTools)
{
    public const string DiscriminatorValue = "http";
    public override string Type => DiscriminatorValue;
    public required string Url { get; init; } = url;
    public required IReadOnlyDictionary<string, string> Headers { get; init; } = headers;
}

// System.Text.Json's built-in [JsonPolymorphic] support requires the type discriminator to be
// the first property in the JSON object, which real-world MCP server configuration (e.g. "name"
// before "type") doesn't guarantee. This converter reads the discriminator regardless of position.
class McpServerJsonConverter : JsonConverter<McpServer>
{
    public override McpServer? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (!root.TryGetProperty("type", out var typeProperty) && !root.TryGetProperty("Type", out typeProperty))
        {
            throw new JsonException("""MCP server entries must have a "type" property of "stdio" or "http".""");
        }

        var discriminator = typeProperty.GetString();
        return discriminator switch
        {
            StdioMcpServer.DiscriminatorValue => root.Deserialize<StdioMcpServer>(options),
            HttpMcpServer.DiscriminatorValue => root.Deserialize<HttpMcpServer>(options),
            _ => throw new JsonException($"""Unknown MCP server "type" value "{discriminator}". Expected "stdio" or "http".""")
        };
    }

    public override void Write(Utf8JsonWriter writer, McpServer value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, value.GetType(), options);
}

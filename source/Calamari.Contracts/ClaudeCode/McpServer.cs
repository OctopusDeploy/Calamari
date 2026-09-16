using System.Text.Json.Serialization;

namespace Octopus.Calamari.Contracts.ClaudeCode;

// Deserializers of this type should enable `JsonSerializerOptions.AllowOutOfOrderMetadataProperties`
// otherwise the "type" discriminator must be the first property in each object.
// The discriminator name is matched case-sensitively regardless of PropertyNameCaseInsensitive.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StdioMcpServer), StdioMcpServer.DiscriminatorValue)]
[JsonDerivedType(typeof(HttpMcpServer), HttpMcpServer.DiscriminatorValue)]
public abstract class McpServer
{
    public required string Name { get; init; }
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();
    public IReadOnlyCollection<string> AllowedTools { get; init; } = [];
}

public class StdioMcpServer : McpServer
{
    public const string DiscriminatorValue = "stdio";

    public required string Command { get; init; }
    public IReadOnlyList<string> Args { get; init; } = [];
}

public class HttpMcpServer : McpServer
{
    public const string DiscriminatorValue = "http";

    public required string Url { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}

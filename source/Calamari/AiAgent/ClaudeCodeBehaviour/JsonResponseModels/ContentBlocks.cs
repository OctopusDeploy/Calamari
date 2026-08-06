using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;

public record ContentBlock
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

public record TextContentBlock : ContentBlock
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

public record ThinkingContentBlock : ContentBlock
{
    [JsonPropertyName("thinking")]
    public string? Thinking { get; init; }

    [JsonPropertyName("signature")]
    public string? Signature { get; init; }
}

public record RedactedThinkingContentBlock : ContentBlock;

public record ToolUseContentBlock : ContentBlock
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("input")]
    public JsonElement? Input { get; init; }

    [JsonPropertyName("caller")]
    public ToolUseCaller? Caller { get; init; }
}

public record ToolUseCaller
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

public record ToolResultContentBlock : ContentBlock
{
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("is_error")]
    public bool? IsError { get; init; }

    [JsonPropertyName("content")]
    public JsonElement? Content { get; init; }
}

public record ServerToolUseContentBlock : ContentBlock
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public record ServerToolResultContentBlock : ContentBlock
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;

public record UserStreamEvent : StreamEvent
{
    [JsonPropertyName("message")]
    public StreamMessage? Message { get; init; }

    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("isSynthetic")]
    public bool? IsSynthetic { get; init; }
}

using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;

public record AssistantStreamEvent : StreamEvent
{
    [JsonPropertyName("message")]
    public StreamMessage? Message { get; init; }

    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }
}

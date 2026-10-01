using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;

public record StreamMessage
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; init; }

    [JsonPropertyName("usage")]
    public MessageUsageInfo? Usage { get; init; }

    [JsonPropertyName("content")]
    public JsonElement[]? Content { get; init; }
}

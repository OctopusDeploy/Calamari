using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;

public record ResultStreamEvent : StreamEvent
{
    [JsonPropertyName("subtype")]
    public string? Subtype { get; init; }

    [JsonPropertyName("is_error")]
    public bool? IsError { get; init; }

    [JsonPropertyName("result")]
    public string? Result { get; init; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    [JsonPropertyName("cost_usd")]
    public double? CostUsd { get; init; }

    [JsonPropertyName("total_cost_usd")]
    public double? TotalCostUsd { get; init; }

    [JsonPropertyName("duration_ms")]
    public double? DurationMs { get; init; }

    [JsonPropertyName("duration_api_ms")]
    public double? DurationApiMs { get; init; }

    [JsonPropertyName("num_turns")]
    public int? NumTurns { get; init; }

    [JsonPropertyName("usage")]
    public ResultUsageInfo? Usage { get; init; }

    [JsonPropertyName("modelUsage")]
    public IReadOnlyDictionary<string, ModelUsageInfo>? ModelUsage { get; init; }

    [JsonPropertyName("permission_denials")]
    public IReadOnlyList<PermissionDenial>? PermissionDenials { get; init; }

    [JsonPropertyName("fast_mode_state")]
    public string? FastModeState { get; init; }
}

public record PermissionDenial
{
    [JsonPropertyName("tool_name")]
    public string? ToolName { get; init; }

    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; init; }

    [JsonPropertyName("tool_input")]
    public JsonElement? ToolInput { get; init; }
}

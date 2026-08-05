using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;

public record MessageUsageInfo
{
    [JsonPropertyName("input_tokens")]
    public int? InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; init; }

    [JsonPropertyName("cache_read_input_tokens")]
    public int? CacheReadInputTokens { get; init; }

    [JsonPropertyName("cache_creation_input_tokens")]
    public int? CacheCreationInputTokens { get; init; }

    [JsonPropertyName("cache_creation")]
    public CacheCreationInfo? CacheCreation { get; init; }

    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; init; }

    [JsonPropertyName("inference_geo")]
    public string? InferenceGeo { get; init; }
}

public record ResultUsageInfo
{
    [JsonPropertyName("input_tokens")]
    public int? InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; init; }

    [JsonPropertyName("cache_read_input_tokens")]
    public int? CacheReadInputTokens { get; init; }

    [JsonPropertyName("cache_creation_input_tokens")]
    public int? CacheCreationInputTokens { get; init; }

    [JsonPropertyName("server_tool_use")]
    public ServerToolUseUsage? ServerToolUse { get; init; }

    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; init; }

    [JsonPropertyName("cache_creation")]
    public CacheCreationInfo? CacheCreation { get; init; }

    [JsonPropertyName("inference_geo")]
    public string? InferenceGeo { get; init; }

    [JsonPropertyName("speed")]
    public string? Speed { get; init; }
}

public record ModelUsageInfo
{
    [JsonPropertyName("inputTokens")]
    public int? InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public int? OutputTokens { get; init; }

    [JsonPropertyName("cacheReadInputTokens")]
    public int? CacheReadInputTokens { get; init; }

    [JsonPropertyName("cacheCreationInputTokens")]
    public int? CacheCreationInputTokens { get; init; }

    [JsonPropertyName("webSearchRequests")]
    public int? WebSearchRequests { get; init; }

    [JsonPropertyName("costUSD")]
    public double? CostUsd { get; init; }

    [JsonPropertyName("contextWindow")]
    public int? ContextWindow { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; init; }
}

public record ServerToolUseUsage
{
    [JsonPropertyName("web_search_requests")]
    public int? WebSearchRequests { get; init; }

    [JsonPropertyName("web_fetch_requests")]
    public int? WebFetchRequests { get; init; }
}

public record CacheCreationInfo
{
    [JsonPropertyName("ephemeral_5m_input_tokens")]
    public int? Ephemeral5mInputTokens { get; init; }

    [JsonPropertyName("ephemeral_1h_input_tokens")]
    public int? Ephemeral1hInputTokens { get; init; }
}

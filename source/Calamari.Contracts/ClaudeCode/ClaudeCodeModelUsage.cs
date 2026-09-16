using System.Text.Json.Serialization;

namespace Octopus.Calamari.Contracts.ClaudeCode;

public record ClaudeCodeModelUsage
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("inputTokens")]
    public int? InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public int? OutputTokens { get; init; }

    [JsonPropertyName("cacheReadInputTokens")]
    public int? CacheReadInputTokens { get; init; }

    [JsonPropertyName("cacheCreationInputTokens")]
    public int? CacheCreationInputTokens { get; init; }

    [JsonPropertyName("costUsd")]
    public double? CostUsd { get; init; }
}

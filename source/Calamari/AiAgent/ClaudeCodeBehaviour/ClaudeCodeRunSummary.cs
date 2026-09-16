namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public record ClaudeCodeRunSummary
{
    public double? CostUsd { get; init; }
    public double? TotalCostUsd { get; init; }
    public double? DurationMs { get; init; }
    public double? DurationApiMs { get; init; }
    public int? NumTurns { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public int? CacheReadInputTokens { get; init; }
    public int? CacheCreationInputTokens { get; init; }
}

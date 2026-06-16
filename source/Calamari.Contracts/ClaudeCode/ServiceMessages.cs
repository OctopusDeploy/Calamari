using System;

namespace Octopus.Calamari.Contracts.ClaudeCode;

public static class ClaudeCodeServiceMessages
{
    public static class Usage
    {
        public const string Name = "claude-code-usage";

        public const string CostUsdAttribute = "costUsd";
        public const string TotalCostUsdAttribute = "totalCostUsd";
        public const string DurationMsAttribute = "durationMs";
        public const string DurationApiMsAttribute = "durationApiMs";
        public const string NumTurnsAttribute = "numTurns";
        public const string InputTokensAttribute = "inputTokens";
        public const string OutputTokensAttribute = "outputTokens";
        public const string CacheReadInputTokensAttribute = "cacheReadInputTokens";
        public const string CacheCreationInputTokensAttribute = "cacheCreationInputTokens";
        public const string ModelAttribute = "model"; //TODO: @team-modern-deployments ensure we capture the model used
    }
}
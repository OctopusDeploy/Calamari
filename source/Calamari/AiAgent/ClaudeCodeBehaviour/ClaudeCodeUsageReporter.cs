using System.Collections.Generic;
using System.Text.Json;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Octopus.Calamari.Contracts.ClaudeCode;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class ClaudeCodeUsageReporter
{
    readonly Dictionary<string, ClaudeCodeModelUsage> modelUsage = new();
    readonly Dictionary<string, string> summaryProperties = new();

    public void AddModelUsage(IEnumerable<ClaudeCodeModelUsage> usages)
    {
        foreach (var usage in usages)
        {
            modelUsage[usage.Model] = modelUsage.TryGetValue(usage.Model, out var existing)
                ? Merge(existing, usage)
                : usage;
        }
    }

    public void SetRunSummary(ClaudeCodeRunSummary summary)
    {
        SetProperty(ClaudeCodeServiceMessages.Usage.CostUsdAttribute, summary.CostUsd?.ToString("F6"));
        SetProperty(ClaudeCodeServiceMessages.Usage.TotalCostUsdAttribute, summary.TotalCostUsd?.ToString("F6"));
        SetProperty(ClaudeCodeServiceMessages.Usage.DurationMsAttribute, summary.DurationMs?.ToString("F0"));
        SetProperty(ClaudeCodeServiceMessages.Usage.DurationApiMsAttribute, summary.DurationApiMs?.ToString("F0"));
        SetProperty(ClaudeCodeServiceMessages.Usage.NumTurnsAttribute, summary.NumTurns?.ToString());
        SetProperty(ClaudeCodeServiceMessages.Usage.InputTokensAttribute, summary.InputTokens?.ToString());
        SetProperty(ClaudeCodeServiceMessages.Usage.OutputTokensAttribute, summary.OutputTokens?.ToString());
        SetProperty(ClaudeCodeServiceMessages.Usage.CacheReadInputTokensAttribute, summary.CacheReadInputTokens?.ToString());
        SetProperty(ClaudeCodeServiceMessages.Usage.CacheCreationInputTokensAttribute, summary.CacheCreationInputTokens?.ToString());
    }

    void SetProperty(string key, string? value)
    {
        if (value != null)
            summaryProperties[key] = value;
    }

    public void WriteServiceMessage(ILog log)
    {
        if (modelUsage.Count == 0 && summaryProperties.Count == 0)
            return;

        var properties = new Dictionary<string, string>(summaryProperties);

        if (modelUsage.Count > 0)
        {
            var usageList = new List<ClaudeCodeModelUsage>(modelUsage.Values);
            properties[ClaudeCodeServiceMessages.Usage.ModelUsageAttribute] = JsonSerializer.Serialize(usageList);
        }

        log.WriteServiceMessage(new ServiceMessage(ClaudeCodeServiceMessages.Usage.Name, properties));
    }

    static ClaudeCodeModelUsage Merge(ClaudeCodeModelUsage a, ClaudeCodeModelUsage b) => new()
    {
        Model = a.Model,
        InputTokens = Sum(a.InputTokens, b.InputTokens),
        OutputTokens = Sum(a.OutputTokens, b.OutputTokens),
        CacheReadInputTokens = Sum(a.CacheReadInputTokens, b.CacheReadInputTokens),
        CacheCreationInputTokens = Sum(a.CacheCreationInputTokens, b.CacheCreationInputTokens),
        CostUsd = Sum(a.CostUsd, b.CostUsd),
    };

    static int? Sum(int? x, int? y) => x.HasValue || y.HasValue ? (x ?? 0) + (y ?? 0) : null;

    static double? Sum(double? x, double? y) => x.HasValue || y.HasValue ? (x ?? 0) + (y ?? 0) : null;
}

using System.Collections.Generic;
using System.Text.Json;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Octopus.Calamari.Contracts.ClaudeCode;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

/// <summary>
/// Accumulates Claude Code model usage from every source in a single run
/// (the prompt-injection check and the main agent invocation) and emits a
/// single <c>claude-code-usage</c> service message. Usage for the same model
/// is summed into one entry so the receiver sees exactly one
/// <see cref="ClaudeCodeModelUsage"/> per model.
/// </summary>
public class ClaudeCodeUsageReporter
{
    readonly Dictionary<string, ClaudeCodeModelUsage> modelUsage = new();
    readonly Dictionary<string, string> summaryProperties = new();

    /// <summary>
    /// Adds per-model usage, summing token/cost values into any existing entry for the same model.
    /// </summary>
    public void AddModelUsage(IEnumerable<ClaudeCodeModelUsage> usages)
    {
        foreach (var usage in usages)
        {
            modelUsage[usage.Model] = modelUsage.TryGetValue(usage.Model, out var existing)
                ? Merge(existing, usage)
                : usage;
        }
    }

    /// <summary>
    /// Records the run-level summary (cost, duration, turns, aggregate tokens) captured from the
    /// main agent's result event, formatting each populated field into its service message attribute.
    /// The last write wins.
    /// </summary>
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

    /// <summary>
    /// Emits the aggregated <c>claude-code-usage</c> service message. Does nothing when no usage
    /// has been recorded, so failure paths that never produced usage don't emit an empty message.
    /// </summary>
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

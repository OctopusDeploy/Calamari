using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class ClaudeCommandArgsBuilder
{
    string? prompt;
    string? model;
    string? systemPromptFile;
    string? mcpConfigPath;
    string? debugLogPath;
    int maxTurns = 10;
    decimal? maxBudgetUsd;
    IReadOnlyList<string>? allowedTools;
    string? effort;
    SandboxMode? sandboxMode;
    string? srtSettingsPath;

    public ClaudeCommandArgsBuilder WithPrompt(string prompt)
    {
        this.prompt = prompt;
        return this;
    }

    public ClaudeCommandArgsBuilder WithDebugLogPath(string debugLogPath)
    {
        this.debugLogPath = debugLogPath;
        return this;
    }

    public ClaudeCommandArgsBuilder WithModel(string model)
    {
        this.model = model;
        return this;
    }

    public ClaudeCommandArgsBuilder WithSystemPromptFile(string systemPromptFile)
    {
        this.systemPromptFile = systemPromptFile;
        return this;
    }

    public ClaudeCommandArgsBuilder WithMaxTurns(int maxTurns)
    {
        this.maxTurns = maxTurns;
        return this;
    }

    public ClaudeCommandArgsBuilder WithMcpConfigPath(string mcpConfigPath)
    {
        this.mcpConfigPath = mcpConfigPath;
        return this;
    }

    public ClaudeCommandArgsBuilder WithMaxBudgetUsd(decimal budgetUsd)
    {
        this.maxBudgetUsd = budgetUsd;
        return this;
    }

    public ClaudeCommandArgsBuilder WithAllowedTools(IReadOnlyList<string> tools)
    {
        this.allowedTools = tools;
        return this;
    }

    public ClaudeCommandArgsBuilder WithEffort(string effort)
    {
        this.effort = effort;
        return this;
    }

    public ClaudeCommandArgsBuilder WithSandboxMode(SandboxMode value)
    {
        sandboxMode = value;
        return this;
    }

    public SandboxMode SandboxMode => sandboxMode ?? SandboxMode.None;

    public ClaudeCommandArgsBuilder WithSrtSettingsPath(string? value)
    {
        srtSettingsPath = value;
        return this;
    }

    public string? SrtSettingsPath => srtSettingsPath;

    public string Build()
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new InvalidOperationException("A prompt is required. Call WithPrompt() before Build().");

        var args = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(model))
        {
            args.Append(" --model ");
            args.Append(EscapeArg(model));
        }

        args.Append(" --bare");
        args.Append(" --strict-mcp-config");
        args.Append(" --output-format stream-json");
        args.Append(" --verbose");
        args.Append(" --permission-mode dontAsk");
        args.Append(" --no-session-persistence");

        if (!string.IsNullOrWhiteSpace(debugLogPath))
        {
            args.Append(" --debug-file ");
            args.Append(EscapeArg(debugLogPath));
        }

        if (!string.IsNullOrWhiteSpace(mcpConfigPath))
        {
            args.Append(" --mcp-config ");
            args.Append(EscapeArg(mcpConfigPath));
        }

        if (!string.IsNullOrWhiteSpace(systemPromptFile))
        {
            args.Append(" --system-prompt-file ");
            args.Append(EscapeArg(systemPromptFile));
        }

        if (allowedTools != null && allowedTools.Count > 0)
        {
            args.Append(" --allowedTools ");
            args.Append(string.Join(",", allowedTools));
        }

        args.Append($" --max-turns {maxTurns}");

        if (maxBudgetUsd.HasValue)
            args.Append($" --max-budget-usd {maxBudgetUsd.Value.ToString(CultureInfo.InvariantCulture)}");

        if (!string.IsNullOrWhiteSpace(effort))
            args.Append($" --effort {effort}");

        args.Append(" -p ");
            args.Append(EscapeArg(prompt));

        return args.ToString();
    }

    static string EscapeArg(string arg)
    {
        if (arg.IndexOfAny(new[] { ' ', '"', '\\' }) < 0)
            return arg;

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}

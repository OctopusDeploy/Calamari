using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Calamari.AiAgent.Behaviours
{
    public class ClaudeCommandArgsBuilder
    {
        string? prompt;
        string? model;
        string? systemPrompt;
        string? mcpConfigPath;
        string? appendSystemPromptFile;
        int maxTurns = 10;
        decimal? maxBudgetUsd;
        IReadOnlyList<string>? allowedTools;
        string? effort;

        public ClaudeCommandArgsBuilder WithPrompt(string prompt)
        {
            this.prompt = prompt;
            return this;
        }

        public ClaudeCommandArgsBuilder WithModel(string model)
        {
            this.model = model;
            return this;
        }

        public ClaudeCommandArgsBuilder WithSystemPrompt(string systemPrompt)
        {
            this.systemPrompt = systemPrompt;
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

        public ClaudeCommandArgsBuilder WithAppendSystemPromptFile(string path)
        {
            this.appendSystemPromptFile = path;
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

        public string Build()
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new InvalidOperationException("A prompt is required. Call WithPrompt() before Build().");

            var args = new StringBuilder();

            args.Append(" --model ");
            args.Append(EscapeArg(model ?? "claude-sonnet-4-20250514"));
            args.Append(" --bare");
            args.Append(" --strict-mcp-config");
            args.Append(" --output-format stream-json");
            args.Append(" --verbose");
            args.Append(" --permission-mode dontAsk");
            args.Append(" --no-session-persistence");

            if (!string.IsNullOrWhiteSpace(mcpConfigPath))
            {
                args.Append(" --mcp-config ");
                args.Append(EscapeArg(mcpConfigPath));
            }

            if (!string.IsNullOrWhiteSpace(appendSystemPromptFile))
            {
                args.Append(" --append-system-prompt-file ");
                args.Append(EscapeArg(appendSystemPromptFile));
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

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                args.Append(" --system-prompt ");
                args.Append(EscapeArg(systemPrompt));
            }

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
}

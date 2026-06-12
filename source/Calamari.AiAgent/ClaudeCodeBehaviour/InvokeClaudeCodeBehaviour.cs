using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.ClaudeCodeBehaviour
{
    public class InvokeClaudeCodeBehaviour : IDeployBehaviour
    {
        readonly ILog log;
        readonly INonSensitiveVariables nonSensitiveVariables;

        public InvokeClaudeCodeBehaviour(ILog log, INonSensitiveVariables nonSensitiveVariables)
        {
            this.log = log;
            this.nonSensitiveVariables = nonSensitiveVariables;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
            //var provider = context.Variables.Get(SpecialVariables.Action.AiAgent.Provider);
            //return provider == "ClaudeCode";
        }

        public async Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;

            var prompt = variables.Get(SpecialVariables.Action.AiAgent.Prompt);
            if (string.IsNullOrWhiteSpace(prompt))
                throw new CommandException($"Variable '{SpecialVariables.Action.AiAgent.Prompt}' is required but was not provided.");

            var apiToken = variables.Get(SpecialVariables.Action.AiAgent.ApiToken);
            if (string.IsNullOrWhiteSpace(apiToken))
                throw new CommandException($"Variable '{SpecialVariables.Action.AiAgent.ApiToken}' is required but was not provided.");

            var model = variables.Get(SpecialVariables.Action.AiAgent.Model);
            if (string.IsNullOrWhiteSpace(model))
                model = "claude-sonnet-4-20250514";

            log.Info($"Invoking Claude Code CLI with model '{model}'...");

            var runAs = BuildRunAs(variables);

            var argsBuilder = new ClaudeCommandArgsBuilder()
                              .WithPrompt(prompt)
                              .WithModel(model);

            var maxTurns = variables.GetInt32(SpecialVariables.Action.AiAgent.MaxTurns);
            if (maxTurns.HasValue)
                argsBuilder.WithMaxTurns(maxTurns.Value);

            var maxBudgetUsdRaw = variables.Get(SpecialVariables.Action.AiAgent.MaxBudgetUsd);
            if (!string.IsNullOrWhiteSpace(maxBudgetUsdRaw)
                && decimal.TryParse(maxBudgetUsdRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var budgetUsd))
                argsBuilder.WithMaxBudgetUsd(budgetUsd);

            var effort = variables.Get(SpecialVariables.Action.AiAgent.Effort);
            if (!string.IsNullOrWhiteSpace(effort))
                argsBuilder.WithEffort(effort);

            using var tempDir = TemporaryDirectory.Create();
            var workingDir = tempDir.DirectoryPath;
            log.Verbose($"Claude Code working directory: {workingDir}");

            // TODO: THis should be moved up higher in execution Chain.
            var cancellationToken = new CancellationTokenSource();
            var mcpWriter = new McpWriter(variables);
            var mcpConfig = mcpWriter.SetupMcpConfig(workingDir);
            
            var allowedTools = AllowedTools(variables);
            allowedTools.AddRange(mcpWriter.GetAllowedTools());
            argsBuilder = argsBuilder.WithAllowedTools(allowedTools);
            
            new SkillsWriter(variables).SetupSkills(workingDir);
            SetupDeploymentVariables(workingDir);
            argsBuilder.WithSystemPromptFile(new SystemPromptWriter().WriteSystemPromptFile(workingDir));
            argsBuilder.WithMcpConfigPath(mcpConfig);
            
            var customEnvVars = new Dictionary<string, string>
            {
                ["ANTHROPIC_API_KEY"] = apiToken,
            };
            
            var response = await new ClaudeCodeCliRunner(log).RunAsync(argsBuilder, customEnvVars,  runAs, workingDir,
                cancellationToken.Token);

            Log.SetOutputVariable(SpecialVariables.Action.AiAgent.Response, response, variables);
            log.Info("Claude Code invocation complete.");
        }

        static List<string> AllowedTools(IVariables variables)
        {
            var defaultAllowedTools = new[] { "Bash", "Read", "Write", "Edit", "Glob", "Grep", "WebSearch", "WebFetch" };
            var allowedToolsRaw = variables.Get(SpecialVariables.Action.AiAgent.AllowedTools);
            var allowedTools = new List<string>(!string.IsNullOrWhiteSpace(allowedToolsRaw)
                ? allowedToolsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : defaultAllowedTools);
            return allowedTools;
        }

        void SetupDeploymentVariables(string workingDir)
        {
            var json = JsonSerializer.Serialize(nonSensitiveVariables, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(workingDir, "deployment-variables.json"), json);
        }

        static ProcessCredentials? BuildRunAs(IVariables variables)
        {
            var username = variables.Get(SpecialVariables.Action.AiAgent.RunAsUsername);
            if (string.IsNullOrWhiteSpace(username))
                return null;

            return new ProcessCredentials
            {
                Username = username,
                Password = variables.Get(SpecialVariables.Action.AiAgent.RunAsPassword),
            };
        }
    }
}
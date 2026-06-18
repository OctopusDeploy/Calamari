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
using Octopus.CoreUtilities.Extensions;

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

        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;

            var prompt = variables.Get(SpecialVariables.Action.Claude.Prompt);
            if (string.IsNullOrWhiteSpace(prompt))
                throw new CommandException($"Variable '{SpecialVariables.Action.Claude.Prompt}' is required but was not provided.");

            var apiToken = variables.Get(SpecialVariables.Action.Claude.ApiToken);
            if (string.IsNullOrWhiteSpace(apiToken))
                throw new CommandException($"Variable '{SpecialVariables.Action.Claude.ApiToken}' is required but was not provided.");

            

            var runAs = BuildRunAs(variables);

            var argsBuilder = new ClaudeCommandArgsBuilder()
                              .WithPrompt(prompt);
                
            var model = variables.Get(SpecialVariables.Action.Claude.Model);
            if (!string.IsNullOrWhiteSpace(model))      
                argsBuilder = argsBuilder.WithModel(model);

            var maxTurns = variables.GetInt32(SpecialVariables.Action.Claude.MaxTurns);
            if (maxTurns.HasValue)
                argsBuilder.WithMaxTurns(maxTurns.Value);

            var maxBudgetUsdRaw = variables.Get(SpecialVariables.Action.Claude.MaxBudgetUsd);
            if (!string.IsNullOrWhiteSpace(maxBudgetUsdRaw)
                && decimal.TryParse(maxBudgetUsdRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var budgetUsd))
                argsBuilder.WithMaxBudgetUsd(budgetUsd);

            var effort = variables.Get(SpecialVariables.Action.Claude.Effort);
            if (!string.IsNullOrWhiteSpace(effort))
                argsBuilder.WithEffort(effort);

            var sandboxMode = ResolveSandboxMode(variables);

            using var tempDir = TemporaryDirectory.Create(); 
            //TODO: Fiddling with workdir for user perms
            //new TemporaryDirectory($"/tmp/{Guid.NewGuid():N}");
            //Directory.CreateDirectory(tempDir.DirectoryPath);

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

            if (sandboxMode == SandboxMode.Bash)
            {
                BashSandboxSettingsWriter.Write(workingDir, variables);
            }
            else if (sandboxMode == SandboxMode.Srt)
            {
                // sandbox-runtime < 0.0.55 fails open on the network — verify before relying on it.
                SrtVersionGuard.Ensure(log);
                // The srt settings path travels on the args builder; the runner reads it to wrap claude.
                argsBuilder.WithSrtSettingsPath(SrtSettingsWriter.Write(workingDir, variables));
            }

            argsBuilder.WithSystemPromptFile(new SystemPromptWriter().WriteSystemPromptFile(workingDir));
            argsBuilder.WithMcpConfigPath(mcpConfig);

            // The agent does NOT inherit the worker's environment wholesale — it is the main carrier
            // of injected secrets. Only an allowlist (plus step opt-ins) is passed through, with the
            // freshly-injected token and the subprocess credential scrub always set.
            var environment = AgentEnvironment.Build(
                Environment.GetEnvironmentVariables(),
                PassThroughEnvironmentVariables(variables),
                new Dictionary<string, string>
                {
                    ["ANTHROPIC_API_KEY"] = apiToken,
                    // Strip Anthropic/cloud credentials from Bash, hook, and MCP subprocess environments.
                    ["CLAUDE_CODE_SUBPROCESS_ENV_SCRUB"] = "1",
                });

            var response = await new ClaudeCodeCliRunner(log).RunAsync(argsBuilder, environment, runAs, workingDir,
                context.CurrentDirectory,
                sandboxMode,
                cancellationToken.Token);

            Log.SetOutputVariable(SpecialVariables.Action.Claude.Response, response, variables);
            log.Info("Claude Code invocation complete.");
        }

        static string[] AllowedTools(IVariables variables)
        {
            var allowedToolsRaw = variables.Get(SpecialVariables.Action.Claude.AllowedTools) ?? "";
            return allowedToolsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Names of worker environment variables the step explicitly opts in to pass through to the agent.
        static string[] PassThroughEnvironmentVariables(IVariables variables)
        {
            var raw = variables.Get(SpecialVariables.Action.Claude.PassEnvironmentVariables) ?? "";
            return raw.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        void SetupDeploymentVariables(string workingDir)
        {
            var json = JsonSerializer.Serialize(nonSensitiveVariables, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(workingDir, "deployment-variables.json"), json);
        }

        static SandboxMode ResolveSandboxMode(IVariables variables)
        {
            var raw = variables.Get(SpecialVariables.Action.Claude.SandboxMode);
            if (string.IsNullOrWhiteSpace(raw))
                return SandboxMode.None;

            if (Enum.TryParse<SandboxMode>(raw, ignoreCase: true, out var mode))
            {
                return mode;
            }

            throw new CommandException($"Unknown value '{raw}' for '{SpecialVariables.Action.Claude.SandboxMode}'. Expected one of: None, Bash, Srt.");
        }

        static ProcessCredentials? BuildRunAs(IVariables variables)
        {
            var username = variables.Get(SpecialVariables.Action.Claude.RunAsUsername);
            if (string.IsNullOrWhiteSpace(username))
                return null;

            return new ProcessCredentials
            {
                Username = username,
                Password = variables.Get(SpecialVariables.Action.Claude.RunAsPassword),
            };
        }
    }
}
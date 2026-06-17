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

            var useSandbox = variables.GetFlag(SpecialVariables.Action.Claude.Sandbox);
            var wrapperCommand = variables.Get(SpecialVariables.Action.Claude.WrapperCommand);

            if (useSandbox && !string.IsNullOrWhiteSpace(wrapperCommand))
                throw new CommandException($"'{SpecialVariables.Action.Claude.Sandbox}' and '{SpecialVariables.Action.Claude.WrapperCommand}' cannot both be set.");

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
            if (useSandbox)
                SetupSandboxSettings(workingDir);
            if (!string.IsNullOrWhiteSpace(wrapperCommand))
                SetupSrtSettings();
            argsBuilder.WithSystemPromptFile(new SystemPromptWriter().WriteSystemPromptFile(workingDir));
            argsBuilder.WithMcpConfigPath(mcpConfig);
            
            var customEnvVars = new Dictionary<string, string>
            {
                ["ANTHROPIC_API_KEY"] = apiToken,
            };
            
            var response = await new ClaudeCodeCliRunner(log).RunAsync(argsBuilder, customEnvVars, runAs, workingDir,
                context.CurrentDirectory,
                wrapperCommand,
                cancellationToken.Token);

            Log.SetOutputVariable(SpecialVariables.Action.Claude.Response, response, variables);
            log.Info("Claude Code invocation complete.");
        }

        static string[] AllowedTools(IVariables variables)
        {
            var allowedToolsRaw = variables.Get(SpecialVariables.Action.Claude.AllowedTools) ?? "";
            return allowedToolsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        void SetupDeploymentVariables(string workingDir)
        {
            var json = JsonSerializer.Serialize(nonSensitiveVariables, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(workingDir, "deployment-variables.json"), json);
        }

        static void SetupSrtSettings()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "Calamari.AiAgent.ClaudeCodeBehaviour.DefaultContext.srt-settings.json";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                               ?? throw new Exception("Could not find embedded srt-settings.json resource.");
            using var reader = new StreamReader(stream);
            var destPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".srt-settings.json");
            File.WriteAllText(destPath, reader.ReadToEnd());
        }

        static void SetupSandboxSettings(string workingDir)
        {
            var claudeDir = Directory.CreateDirectory(Path.Combine(workingDir, ".claude"));
            File.WriteAllText(Path.Combine(claudeDir.FullName, "settings.json"), """{"sandbox":{"enabled":true}}""");
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
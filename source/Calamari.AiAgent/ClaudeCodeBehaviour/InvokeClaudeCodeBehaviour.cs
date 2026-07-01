using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Calamari.AiAgent.ClaudeCodeBehaviour.InjectionCheck;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class InvokeClaudeCodeBehaviour : IDeployBehaviour
{
    readonly ILog log;
    readonly INonSensitiveVariables nonSensitiveVariables;
    readonly ClaudeSettingsWriter settingsWriter;

    public InvokeClaudeCodeBehaviour(ILog log, INonSensitiveVariables nonSensitiveVariables, ClaudeSettingsWriter settingsWriter)
    {
        this.log = log;
        this.nonSensitiveVariables = nonSensitiveVariables;
        this.settingsWriter = settingsWriter;
    }

    public bool IsEnabled(RunningDeployment context) => true;

    public async Task Execute(RunningDeployment context)
    {
        var variables = context.Variables;

        var prompt = variables.Get(SpecialVariables.Action.Claude.Prompt);
        if (string.IsNullOrWhiteSpace(prompt))
            throw new CommandException($"Variable '{SpecialVariables.Action.Claude.Prompt}' is required but was not provided.");

		// `Octopus.Action.Claude.ApiToken` was previously used during development
        var apiKey = variables.Get(SpecialVariables.Action.Claude.ApiKey) ?? variables.Get("Octopus.Action.Claude.ApiToken");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new CommandException($"Variable '{SpecialVariables.Action.Claude.ApiKey}' is required but was not provided.");

        var argsBuilder = new ClaudeCommandArgsBuilder().WithPrompt(prompt);

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

        argsBuilder.WithPermissionMode(ResolvePermissionMode(variables));

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

        var permissionsJson = variables.Get(SpecialVariables.Action.Claude.Permissions);
        if (!string.IsNullOrWhiteSpace(permissionsJson))
            settingsWriter.Add(new CommandPermissionsSettings(permissionsJson));

        var mcpAllowedTools = new List<string>(mcpWriter.GetAllowedTools());
        if (mcpAllowedTools.Count > 0)
            settingsWriter.Add(new McpServerPermissionsSettings(mcpAllowedTools));

        new SkillsWriter(variables).SetupSkills(workingDir);
        SetupDeploymentVariables(workingDir);

        var sandboxMode = ResolveSandboxMode(variables);
        argsBuilder.WithSandboxMode(sandboxMode);

        switch (sandboxMode)
        {
            case SandboxMode.Bash when RuntimeInformation.IsOSPlatform(OSPlatform.Windows):
                throw new CommandException($"Sandbox mode '{sandboxMode}' is not supported on Windows workers; use 'None' or run on Linux/macOS.");
            case SandboxMode.Bash:
                settingsWriter.Add(new BashSandboxSettings(variables.Get(SpecialVariables.Action.Claude.SandboxSettings)));
                break;
            case SandboxMode.SandboxRuntime when RuntimeInformation.IsOSPlatform(OSPlatform.Windows):
                throw new CommandException($"Sandbox mode '{sandboxMode}' is not supported on Windows workers; use 'None' or run on Linux.");
            case SandboxMode.SandboxRuntime:
                SandboxRuntimeVersionGuard.EnsureAboveMinimum(log);
                argsBuilder.WithSandboxRuntimeSettingsPath(SandboxSettingsWriter.WriteSandboxRuntimeSettings(workingDir, variables));
                break;
            case SandboxMode.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sandboxMode), sandboxMode, null);
        }

        if (settingsWriter.HasSettings)
            argsBuilder.WithSettingsPath(settingsWriter.Write(Path.Combine(workingDir, ".claude", "agent-settings.json")));

        argsBuilder.WithSystemPromptFile(new SystemPromptWriter().WriteSystemPromptFile(workingDir));
        argsBuilder.WithMcpConfigPath(mcpConfig);

        await new PromptInjectionGuard(log, InjectionCheckOptions.Resolve(variables))
            .CheckAsync(workingDir, prompt, apiToken, cancellationToken.Token);

        var claudeConfigDir = Path.Combine(workingDir, ".claude");

        var environment = ClaudeCodeEnvironment.Build(
            ClaudeCodeEnvironment.GetCurrentEnvironmentVariables(),
            PassThroughEnvironmentVariables(variables),
            new Dictionary<string, string>
            {
                ["ANTHROPIC_API_KEY"] = apiKey,
                ["CLAUDE_CODE_SUBPROCESS_ENV_SCRUB"] = "0", // If set, this stops us using auto mode
                ["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1", // Disables the auto-updater, telemetry, error reporting, and feedback surveys
                ["CLAUDE_CODE_DISABLE_BACKGROUND_TASKS"] = "1",
                ["CLAUDE_CODE_DISABLE_CRON"] = "1",
                ["CLAUDE_CONFIG_DIR"] = claudeConfigDir,
            });

        var response = await new ClaudeCodeCliRunner(log).RunAsync(
            argsBuilder,
            environment,
            workingDir,
            context.CurrentDirectory,
            cancellationToken.Token);

        foreach (var artifact in new ArtifactManifestCollector(variables).Collect(workingDir, context.CurrentDirectory))
            log.NewOctopusArtifact(artifact.Path, artifact.Name, artifact.Length);

        Log.SetOutputVariable(SpecialVariables.Action.Claude.Response, response, variables);
        log.Info("Claude Code invocation complete.");
    }

    internal static ClaudePermissionMode ResolvePermissionMode(IVariables variables)
    {
        var raw = variables.Get(SpecialVariables.Action.Claude.PermissionMode);
        if (string.IsNullOrWhiteSpace(raw))
            return ClaudePermissionMode.DontAsk;

        if (Enum.TryParse<ClaudePermissionMode>(raw, ignoreCase: true, out var mode))
            return mode;

        throw new CommandException($"Unknown value '{raw}' for '{SpecialVariables.Action.Claude.PermissionMode}'. Expected one of: {string.Join(", ", Enum.GetNames(typeof(ClaudePermissionMode)))}.");
    }

    static string[] PassThroughEnvironmentVariables(IVariables variables)
    {
        var raw = variables.Get(SpecialVariables.Action.Claude.PassEnvironmentVariables, string.Empty);
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
        if (Enum.TryParse<SandboxMode>(raw, ignoreCase: true, out var mode))
        {
            return mode;
        }

        throw new CommandException($"Unknown value '{raw}' for '{SpecialVariables.Action.Claude.SandboxMode}'. Expected one of: None, Bash, SandboxRuntime.");
    }
}
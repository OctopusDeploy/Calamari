using System.IO;
using System.Text.Json;
using Calamari.Common.Plumbing.Variables;
using ClaudeVariables = Calamari.AiAgent.SpecialVariables.Action.Claude;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public static class BashSandboxSettingsWriter
{
    const string ClaudeDirName = ".claude";
    const string SettingsFileName = "settings.json";

    // Writes the merged bash sandbox settings to <workingDir>/.claude/settings.json and returns the path.
    public static string Write(string workingDir, IVariables variables)
    {
        var claudeDir = Directory.CreateDirectory(Path.Combine(workingDir, ClaudeDirName));
        var destPath = Path.Combine(claudeDir.FullName, SettingsFileName);
        var json = JsonSerializer.Serialize(BuildSettings(variables), SandboxSettingsJsonContext.Default.BashSandboxSettings);
        File.WriteAllText(destPath, json);
        return destPath;
    }

    internal static BashSandboxSettings BuildSettings(IVariables variables)
    {
        var settings = Defaults();
        var sandbox = settings.Sandbox;
        var network = sandbox.Network;
        var filesystem = sandbox.Filesystem;

        network.AllowedDomains = SandboxDefaults.Merge(variables, ClaudeVariables.BashNetworkAllowedDomains, SandboxDefaults.AllowedDomains);
        network.DeniedDomains = SandboxDefaults.Merge(variables, ClaudeVariables.BashNetworkDeniedDomains, network.DeniedDomains);
        filesystem.AllowWrite = SandboxDefaults.Merge(variables, ClaudeVariables.BashFilesystemAllowWrite, SandboxDefaults.AllowWrite);
        filesystem.DenyWrite = SandboxDefaults.Merge(variables, ClaudeVariables.BashFilesystemDenyWrite, filesystem.DenyWrite);
        filesystem.DenyRead = SandboxDefaults.Merge(variables, ClaudeVariables.BashFilesystemDenyRead, SandboxDefaults.DenyRead);
        filesystem.AllowRead = SandboxDefaults.Merge(variables, ClaudeVariables.BashFilesystemAllowRead, filesystem.AllowRead);
        sandbox.ExcludedCommands = SandboxDefaults.Merge(variables, ClaudeVariables.BashExcludedCommands, sandbox.ExcludedCommands);

        return settings;
    }

    // Hardened secure baseline, always retained. Customers extend the allow/deny lists via the step.
    static BashSandboxSettings Defaults() => new()
    {
        Sandbox = new()
        {
            Enabled = true,
            FailIfUnavailable = true,
            AllowUnsandboxedCommands = false,
            Network = new() { AllowedDomains = [..SandboxDefaults.AllowedDomains] },
            Filesystem = new()
            {
                AllowWrite = [..SandboxDefaults.AllowWrite],
                DenyRead = [..SandboxDefaults.DenyRead],
            },
        },
    };
}
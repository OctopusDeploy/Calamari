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

        sandbox.Network = SandboxDefaults.BuildNetworkOptions(
            variables,
            ClaudeVariables.BashNetworkAllowedDomains,
            ClaudeVariables.BashNetworkDeniedDomains,
            ClaudeVariables.BashNetworkAllowUnixSockets,
            ClaudeVariables.BashNetworkAllowAllUnixSockets,
            ClaudeVariables.BashNetworkAllowLocalBinding,
            ClaudeVariables.BashNetworkHttpProxyPort,
            ClaudeVariables.BashNetworkSocksProxyPort);

        sandbox.Filesystem = SandboxDefaults.BuildFilesystemOptions(
            variables,
            ClaudeVariables.BashFilesystemAllowWrite,
            ClaudeVariables.BashFilesystemDenyWrite,
            ClaudeVariables.BashFilesystemDenyRead,
            ClaudeVariables.BashFilesystemAllowRead);

        sandbox.ExcludedCommands = SandboxDefaults.Merge(variables, ClaudeVariables.BashExcludedCommands, sandbox.ExcludedCommands);

        sandbox.AutoAllowBashIfSandboxed = SandboxDefaults.OptionalFlag(variables, ClaudeVariables.BashAutoAllowBashIfSandboxed) ?? sandbox.AutoAllowBashIfSandboxed;
        sandbox.EnableWeakerNestedSandbox = SandboxDefaults.OptionalFlag(variables, ClaudeVariables.BashEnableWeakerNestedSandbox);

        return settings;
    }

    static BashSandboxSettings Defaults() => new()
    {
        Sandbox = new()
        {
            Enabled = true,
            FailIfUnavailable = true,
            AllowUnsandboxedCommands = false,
            AutoAllowBashIfSandboxed = false,
        },
    };
}
using System.IO;
using System.Text.Json;
using Calamari.Common.Plumbing.Variables;
using ClaudeVariables = Calamari.AiAgent.SpecialVariables.Action.Claude;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public static class SrtSettingsWriter
{
    const string ClaudeDirName = ".claude";
    const string SrtSettingsFileName = "srt-settings.json";

    // Writes the merged srt settings to <workingDir>/.claude/srt-settings.json and returns the path.
    public static string Write(string workingDir, IVariables variables)
    {
        var claudeDir = Directory.CreateDirectory(Path.Combine(workingDir, ClaudeDirName));
        var destPath = Path.Combine(claudeDir.FullName, SrtSettingsFileName);
        var json = JsonSerializer.Serialize(BuildSettings(variables), SandboxSettingsJsonContext.Default.SrtSettings);
        File.WriteAllText(destPath, json);
        return destPath;
    }

    internal static SrtSettings BuildSettings(IVariables variables)
        => new()
        {
            Network = SandboxDefaults.BuildNetworkOptions(
                variables,
                ClaudeVariables.SrtNetworkAllowedDomains,
                ClaudeVariables.SrtNetworkDeniedDomains,
                ClaudeVariables.SrtNetworkAllowUnixSockets,
                ClaudeVariables.SrtNetworkAllowAllUnixSockets,
                ClaudeVariables.SrtNetworkAllowLocalBinding,
                ClaudeVariables.SrtNetworkHttpProxyPort,
                ClaudeVariables.SrtNetworkSocksProxyPort),
            Filesystem = SandboxDefaults.BuildFilesystemOptions(
                variables,
                ClaudeVariables.SrtFilesystemAllowWrite,
                ClaudeVariables.SrtFilesystemDenyWrite,
                ClaudeVariables.SrtFilesystemDenyRead,
                ClaudeVariables.SrtFilesystemAllowRead),
            EnableWeakerNestedSandbox = SandboxDefaults.OptionalFlag(variables, ClaudeVariables.SrtEnableWeakerNestedSandbox),
        };
}
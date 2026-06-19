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
    {
        var settings = Defaults();
        var network = settings.Network;
        var filesystem = settings.Filesystem;

        network.AllowedDomains = SandboxDefaults.Merge(variables, ClaudeVariables.SrtNetworkAllowedDomains, SandboxDefaults.AllowedDomains);
        network.DeniedDomains = SandboxDefaults.Merge(variables, ClaudeVariables.SrtNetworkDeniedDomains, network.DeniedDomains);
        filesystem.AllowWrite = SandboxDefaults.Merge(variables, ClaudeVariables.SrtFilesystemAllowWrite, SandboxDefaults.AllowWrite);
        filesystem.DenyWrite = SandboxDefaults.Merge(variables, ClaudeVariables.SrtFilesystemDenyWrite, filesystem.DenyWrite);
        filesystem.DenyRead = SandboxDefaults.Merge(variables, ClaudeVariables.SrtFilesystemDenyRead, SandboxDefaults.DenyRead);
        filesystem.AllowRead = SandboxDefaults.Merge(variables, ClaudeVariables.SrtFilesystemAllowRead, filesystem.AllowRead);

        return settings;
    }

    // Hardened secure baseline, always retained. Customers extend the allow/deny lists via the step.
    static SrtSettings Defaults() => new()
    {
        Network = new() { AllowedDomains = [..SandboxDefaults.AllowedDomains] },
        Filesystem = new()
        {
            AllowWrite = [..SandboxDefaults.AllowWrite],
            DenyRead = [..SandboxDefaults.DenyRead],
        },
    };
}
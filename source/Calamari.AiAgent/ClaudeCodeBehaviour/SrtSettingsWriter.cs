using System.IO;
using System.Linq;
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

        network.AllowedDomains = variables.GetStrings(ClaudeVariables.SrtNetworkAllowedDomains, '\n', '\r').Concat(network.AllowedDomains).Distinct().ToList();
        network.DeniedDomains = variables.GetStrings(ClaudeVariables.SrtNetworkDeniedDomains, '\n', '\r').Concat(network.DeniedDomains).Distinct().ToList();
        filesystem.AllowWrite = variables.GetStrings(ClaudeVariables.SrtFilesystemAllowWrite, '\n', '\r').Concat(filesystem.AllowWrite).Distinct().ToList();
        filesystem.DenyWrite = variables.GetStrings(ClaudeVariables.SrtFilesystemDenyWrite, '\n', '\r').Concat(filesystem.DenyWrite).Distinct().ToList();
        filesystem.DenyRead = variables.GetStrings(ClaudeVariables.SrtFilesystemDenyRead, '\n', '\r').Concat(filesystem.DenyRead).Distinct().ToList();
        filesystem.AllowRead = variables.GetStrings(ClaudeVariables.SrtFilesystemAllowRead, '\n', '\r').Concat(filesystem.AllowRead).Distinct().ToList();

        return settings;
    }

    // Users extend these via the step's allow/deny lists
    static SrtSettings Defaults() => new()
    {
        Network = new() { AllowedDomains = ["api.anthropic.com", "statsig.anthropic.com"] },
        Filesystem = new()
        {
            AllowWrite = [".", "/tmp"],
            DenyRead =
            [
                "~/.ssh", "~/.aws", "~/.azure", "~/.config/gcloud", "~/.kube", "~/.docker",
                "~/.config/gh", "~/.git-credentials", "~/.netrc", "~/.npmrc", "~/.gnupg",
                "~/.claude/.credentials.json",
            ],
        },
    };
}
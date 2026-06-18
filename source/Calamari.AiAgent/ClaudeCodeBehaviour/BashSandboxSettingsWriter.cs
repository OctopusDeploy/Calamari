using System.IO;
using System.Linq;
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

        network.AllowedDomains = variables.GetStrings(ClaudeVariables.BashNetworkAllowedDomains, '\n', '\r').Concat(network.AllowedDomains).Distinct().ToList();
        network.DeniedDomains = variables.GetStrings(ClaudeVariables.BashNetworkDeniedDomains, '\n', '\r').Concat(network.DeniedDomains).Distinct().ToList();
        filesystem.AllowWrite = variables.GetStrings(ClaudeVariables.BashFilesystemAllowWrite, '\n', '\r').Concat(filesystem.AllowWrite).Distinct().ToList();
        filesystem.DenyWrite = variables.GetStrings(ClaudeVariables.BashFilesystemDenyWrite, '\n', '\r').Concat(filesystem.DenyWrite).Distinct().ToList();
        filesystem.DenyRead = variables.GetStrings(ClaudeVariables.BashFilesystemDenyRead, '\n', '\r').Concat(filesystem.DenyRead).Distinct().ToList();
        filesystem.AllowRead = variables.GetStrings(ClaudeVariables.BashFilesystemAllowRead, '\n', '\r').Concat(filesystem.AllowRead).Distinct().ToList();
        sandbox.ExcludedCommands = variables.GetStrings(ClaudeVariables.BashExcludedCommands, '\n', '\r').Concat(sandbox.ExcludedCommands).Distinct().ToList();

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
        },
    };
}
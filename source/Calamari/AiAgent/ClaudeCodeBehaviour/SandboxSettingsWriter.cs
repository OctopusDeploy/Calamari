using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public static class SandboxSettingsWriter
{
    const string SandboxRuntimeSettingsFileName = ".srt-settings.json";

    // Writes the sandbox-runtime settings to <workingDir>/.srt-settings.json and returns the path.
    public static string WriteSandboxRuntimeSettings(string workingDir, IVariables variables)
    {
        var destPath = Path.Combine(workingDir, SandboxRuntimeSettingsFileName);
        File.WriteAllText(destPath, ResolveSettings(variables));
        return destPath;
    }

    static string ResolveSettings(IVariables variables)
    {
        var settings = variables.Get(SpecialVariables.Action.Claude.SandboxSettings);
        if (string.IsNullOrWhiteSpace(settings))
        {
            throw new CommandException($"Sandbox settings are required when a sandbox mode is selected. Expected '{SpecialVariables.Action.Claude.SandboxSettings}' to contain the sandbox settings JSON.");
        }

        return settings;
    }
}

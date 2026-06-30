using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class ClaudeCodeProcessStartInfo
{
    //TODO: Should this be configurable?
    const string ClaudeCodePath = "claude";
    const string SrtPath = "srt";

    internal static string ShellQuote(string value)
    {
        return "'" + value.Replace("'", @"'\''") + "'";
    }

    internal static (string fileName, string arguments) ResolveInvocation(ClaudeCommandArgsBuilder argsBuilder)
    {
        var claudeArgs = argsBuilder.Build();
        return argsBuilder.SandboxMode switch
        {
            SandboxMode.SandboxRuntime when string.IsNullOrWhiteSpace(argsBuilder.SandboxRuntimeSettingsPath)
                => throw new InvalidOperationException("Sandbox runtime mode requires a settings file path."),
            SandboxMode.SandboxRuntime
                => (SrtPath, $"--settings {argsBuilder.SandboxRuntimeSettingsPath} {ClaudeCodePath}{claudeArgs}"),
            _ => (ClaudeCodePath, claudeArgs),
        };
    }

    public Process StartClaudeProcess(
        string workingDir,
        ClaudeCommandArgsBuilder argsBuilder,
        Dictionary<string, string> environmentVariables)
    {
        var (fileName, arguments) = ResolveInvocation(argsBuilder);
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment.Clear();
        foreach (var kvp in environmentVariables)
        {
            startInfo.Environment[kvp.Key] = kvp.Value;
        }

        return Process.Start(startInfo)!;
    }
}
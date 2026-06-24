using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Octopus.CoreUtilities.Extensions;

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

    async Task<Process> StartWindowsProcess(
        string workingDir,
        ProcessCredentials? runAs,
        ClaudeCommandArgsBuilder argsBuilder,
        Dictionary<string, string> environmentVariables)
    {
        var startInfo = StartSimpleProcess(workingDir, argsBuilder, environmentVariables);

        if (runAs != null)
        {
            startInfo.UserName = runAs.Username;
#pragma warning disable CA1416
            if (runAs.Password != null)
                startInfo.PasswordInClearText = runAs.Password;

            if (!string.IsNullOrEmpty(runAs.Domain))
                startInfo.Domain = runAs.Domain;
#pragma warning restore CA1416
        }

        await Task.CompletedTask;

        return Process.Start(startInfo)!;
    }

    static ProcessStartInfo StartSimpleProcess(
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

        return startInfo;
    }

    public async Task<Process> StartClaudeProcess(
        string workingDir,
        ProcessCredentials? runAs,
        ClaudeCommandArgsBuilder argsBuilder,
        Dictionary<string, string> environmentVariables,
        CancellationToken ct)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await StartWindowsProcess(workingDir,
                runAs,
                argsBuilder,
                environmentVariables);
        }

        return await StartMacOrLinuxProcess(workingDir,
            runAs,
            argsBuilder,
            environmentVariables,
            ct);
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Higher up checks enforce the correct OS")]
    async Task<Process> StartMacOrLinuxProcess(
        string workingDir,
        ProcessCredentials? runAs,
        ClaudeCommandArgsBuilder argsBuilder,
        Dictionary<string, string> environmentVariables,
        CancellationToken ct)
    {
        var username = runAs?.Username!;
        if (runAs == null || string.IsNullOrEmpty(username))
        {
            return Process.Start(StartSimpleProcess(workingDir, argsBuilder, environmentVariables))
                   ?? throw new Exception("Failed to start the claude code process");
        }

        var (scriptFileName, scriptArgs) = ResolveInvocation(argsBuilder);
        var filePath = Path.Combine(workingDir, "my-command.sh");
        await File.WriteAllTextAsync(
            filePath,
            $"""
             #!/bin/bash
             cd {workingDir}
             {scriptFileName} {scriptArgs}
             """,
            ct);

        var startInfo = new ProcessStartInfo
        {
            FileName = "script",
            WorkingDirectory = workingDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var argumentList = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? new[] { "-q", "/dev/null", "su", "-m", username, "-c", filePath }
            : new[] { "-qec", "su", "-m", username, "-c", filePath, "/dev/null" };

        startInfo.ArgumentList.AddRange(argumentList);

        startInfo.Environment.Clear();
        foreach (var kvp in environmentVariables)
        {
            startInfo.Environment[kvp.Key] = kvp.Value;
        }

        GrantRunAsAccess(workingDir);

        var process = Process.Start(startInfo)!;

        // TODO: Should just wait as long as it takes to read "Password:" below
        await Task.Delay(1000, ct).WaitAsync(ct);

        // Parse password prompt so consuming code can ignore this initial password check.
        var passwordReq = "Password:".Length;
        var buff = new char[passwordReq];
        await process.StandardOutput.ReadAsync(buff, 0, passwordReq);
        var message = new string(buff);
        if (message != "Password:")
        {
            throw new Exception($"Unexpected startup message: {message}");
        }

        await process.StandardInput.WriteLineAsync(runAs!.Password);
        if (process.StandardOutput.Read() != '\r' || process.StandardOutput.Read() != '\n')
        {
            throw new Exception("Expecting new line");
        }

        return process;
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Only invoked on the macOS/Linux run-as path")]
    static void GrantRunAsAccess(string workingDir)
    {
        var dirMode = UnixFileMode.UserRead
                      | UnixFileMode.UserWrite
                      | UnixFileMode.UserExecute
                      | UnixFileMode.GroupRead
                      | UnixFileMode.GroupWrite
                      | UnixFileMode.GroupExecute
                      | UnixFileMode.OtherRead
                      | UnixFileMode.OtherWrite
                      | UnixFileMode.OtherExecute;

        var fileMode = UnixFileMode.UserRead
                       | UnixFileMode.UserWrite
                       | UnixFileMode.UserExecute
                       | UnixFileMode.GroupRead
                       | UnixFileMode.GroupWrite
                       | UnixFileMode.GroupExecute
                       | UnixFileMode.OtherRead
                       | UnixFileMode.OtherWrite
                       | UnixFileMode.OtherExecute;

        // The sandbox policy must stay readable but never writable by the run-as user or co-tenants.
        var policyMode = UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

        new DirectoryInfo(workingDir).UnixFileMode = dirMode;
        foreach (var dir in Directory.EnumerateDirectories(workingDir, "*", SearchOption.AllDirectories))
        {
            new DirectoryInfo(dir).UnixFileMode = dirMode;
        }

        foreach (var file in Directory.EnumerateFiles(workingDir, "*", SearchOption.AllDirectories))
        {
            File.SetUnixFileMode(file, fileMode);
        }

        var claudeDir = Path.Combine(workingDir, ".claude");
        if (Directory.Exists(claudeDir))
        {
            foreach (var policyFile in Directory.EnumerateFiles(claudeDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                File.SetUnixFileMode(policyFile, policyMode);
            }
        }
    }
}
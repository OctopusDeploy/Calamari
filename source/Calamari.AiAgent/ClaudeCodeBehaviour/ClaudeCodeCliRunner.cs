using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AiAgent.ClaudeCodeBehaviour
{
    public class ClaudeCodeCliRunner(ILog log)
    {

        public async Task<string> RunAsync(ClaudeCommandArgsBuilder argsBuilder,
                                           Dictionary<string, string> customEnvVars,
                                           ProcessCredentials? runAs,
                                           string workingDir,
                                           CancellationToken cancellationToken)
        {
            var verboseLogPath = Path.Combine(Path.GetTempPath(), $"claude-agent-verbose-{Guid.NewGuid():N}.log");
             var debugLogPath = Path.Combine(Path.GetTempPath(), $"claude-agent-debug-{Guid.NewGuid():N}.log");

            log.Verbose($"Claude Code command: claude {argsBuilder.Build()}");

            var runner = new ClaudeCodeProcessStartInfo();
            var process = await runner.StartClaudeProcess(workingDir,
                runAs,
                argsBuilder.WithDebugLogPath(debugLogPath),
                customEnvVars,
                cancellationToken);

            var stdoutTask = Task.Run(() => ProcessLine(process, verboseLogPath, cancellationToken), cancellationToken);
            var stderrTask = Task.Run(() => ProcessError(process), cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new CommandException($"Claude Code exited with code {process.ExitCode}");
            }

            if (File.Exists(debugLogPath))
            {
                var fileInfo = new FileInfo(debugLogPath);
                log.NewOctopusArtifact(debugLogPath, "claude-agent-debug.log", fileInfo.Length);
            }

            if (File.Exists(verboseLogPath))
            {
                var fileInfo = new FileInfo(verboseLogPath);
                log.NewOctopusArtifact(verboseLogPath, "claude-agent-verbose.log", fileInfo.Length);
            }

            return stdoutTask.Result.ToString();
        }

        async Task ProcessError(Process process)
        {
            var buffer = new char[1024];
            int charsRead;
            while ((charsRead = await process.StandardError.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var text = new string(buffer, 0, charsRead);
                log.Verbose(text.TrimEnd());
            }
        }

        async Task<StringBuilder> ProcessLine(Process process, string verboseLogPath, CancellationToken cancellationToken)
        {
            var responseBuilder = new StringBuilder();
            var streamProcessor = new ClaudeCodeStreamProcessor(log, responseBuilder);
            var line = string.Empty;
            int ch;
            while ((ch = process.StandardOutput.Read()) >= 0)
            {
                var c = (char)ch;
                if (c == '\n')
                {
                    line = line.TrimEnd('\r');

                    await File.AppendAllTextAsync(verboseLogPath, line + "\n", cancellationToken);
                    
                     streamProcessor.ProcessLine(line);

                    line = "";
                }
                else
                {
                    line += c;
                }
            }

            if (line != "")
            {
                line = line.TrimEnd('\r');

                await File.AppendAllTextAsync(verboseLogPath, line + "\n", cancellationToken);
                streamProcessor.ProcessLine(line);
            }
            return responseBuilder;
        }
        
/*
        internal static string WriteWrapperScript(ProcessStartInfo startInfo, Dictionary<string, string> customEnvVars, string workingDir)
        {
            var scriptPath = Path.Combine(workingDir, "run-claude.sh");
            var sb = new StringBuilder();
            sb.AppendLine("#!/bin/bash");
            foreach (var kvp in customEnvVars)
                sb.AppendLine($"export {kvp.Key}={ShellQuote(kvp.Value)}");
            sb.AppendLine($"exec {startInfo.FileName} {startInfo.Arguments}");
            File.WriteAllText(scriptPath, sb.ToString());

            // Ensure the target su user can read the working directory and script.
            // The directory may have been created with a restrictive umask (e.g. 077).
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(scriptPath,
                    UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.GroupRead
                    | UnixFileMode.OtherRead);
                File.SetUnixFileMode(workingDir,
                    UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherExecute);
            }

            return scriptPath;
        }
        */
    }

    public record ProcessCredentials
    {
        public required string Username { get; init; }
        public string? Password { get; init; }
        public string? Domain { get; init; }
    }

    public record UserSkill
    {
        public required string Name { get; init; }
        public required string Content { get; init; }
    }

    public record McpServerConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "stdio";

        [JsonPropertyName("command")]
        public required string Command { get; init; }

        [JsonPropertyName("args")]
        public IReadOnlyList<string>? Args { get; init; }

        [JsonPropertyName("env")]
        public IReadOnlyDictionary<string, string>? Env { get; init; }
    }

    public record McpServerEntry
    {
        public string? Name { get; init; }
        public string? Type { get; init; }
        public string? Command { get; init; }
        public IReadOnlyList<string>? Args { get; init; }
        public IReadOnlyDictionary<string, string>? Env { get; init; }
    }
}

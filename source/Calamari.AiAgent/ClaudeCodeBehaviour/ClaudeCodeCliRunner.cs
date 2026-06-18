using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class ClaudeCodeCliRunner(ILog log)
{

    public async Task<string> RunAsync(ClaudeCommandArgsBuilder argsBuilder,
                                       Dictionary<string, string> customEnvVars,
                                       ProcessCredentials? runAs,
                                       string workingDir,
                                       string calamariDir, //RunAs might not be able to access this dir.. but we need to preserve the logs.
                                       CancellationToken cancellationToken)
    {

        var logDir = Directory.CreateDirectory(Path.Combine(workingDir, "log"));
        var verboseLogPath = Path.Combine(logDir.FullName, $"claude-agent-verbose-{Guid.NewGuid():N}.log");
        var debugLogPath = Path.Combine(logDir.FullName, $"claude-agent-debug-{Guid.NewGuid():N}.log");

        // Temporarily here while working out the user process issues
        //await File.Create(debugLogPath).DisposeAsync();

        var (logFileName, logArgs) = ClaudeCodeProcessStartInfo.ResolveInvocation(argsBuilder);
        log.Verbose($"Claude Code command: {logFileName} {logArgs}");

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

        Directory.CreateDirectory(Path.Combine(calamariDir, "log"));
        if (File.Exists(debugLogPath))
        {
            var fileInfo = new FileInfo(debugLogPath);
            var movedFilePath = Path.Combine(calamariDir, "log", fileInfo.Name);
            fileInfo.MoveTo(movedFilePath);
            log.NewOctopusArtifact(movedFilePath, "claude-agent-debug.log", fileInfo.Length);
        }

        if (File.Exists(verboseLogPath))
        {
            var fileInfo = new FileInfo(verboseLogPath);
            var movedFilePath = Path.Combine(calamariDir, "log", fileInfo.Name);
            fileInfo.MoveTo(movedFilePath);
            log.NewOctopusArtifact(movedFilePath, "claude-agent-verbose.log", fileInfo.Length);
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

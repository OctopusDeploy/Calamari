using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AiAgent.Behaviours
{
    public class ClaudeCodeCliRunner
    {
        readonly ILog log;

        public ClaudeCodeCliRunner(ILog log)
        {
            this.log = log;
        }

        public async Task<string> RunAsync(ClaudeCodeOptions options)
        {
            var workingDir = Path.Combine(Path.GetTempPath(), $"claude-agent-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workingDir);
            log.Verbose($"Claude Code working directory: {workingDir}");

            try
            {
                SetupSkills(workingDir);
                SetupMcpConfig(workingDir, options.McpServers);
                return await RunInDirectoryAsync(options, workingDir);
            }
            finally
            {
                try { Directory.Delete(workingDir, recursive: true); }
                catch { /* best effort cleanup */ }
            }
        }

        async Task<string> RunInDirectoryAsync(ClaudeCodeOptions options, string workingDir)
        {
            var args = BuildArguments(options, workingDir);

            var debugLogPath = Path.Combine(Path.GetTempPath(), $"claude-agent-debug-{Guid.NewGuid():N}.log");     
            
            
            args.Append(" --debug-file ");
            args.Append(EscapeArg(debugLogPath));

            var startInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = args.ToString(),
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.Environment["ANTHROPIC_API_KEY"] = options.ApiToken;

            if (options.RunAs != null)
                ApplyCredentials(startInfo, options.RunAs);

            var responseBuilder = new StringBuilder();
            var streamProcessor = new ClaudeCodeStreamProcessor(log, responseBuilder);

            using var process = new Process { StartInfo = startInfo };

            process.Start();

            var stdoutTask = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                {
                    streamProcessor.ProcessLine(line);
                }
            });

            var stderrTask = Task.Run(async () =>
            {
                var buffer = new char[1024];
                int charsRead;
                while ((charsRead = await process.StandardError.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    var text = new string(buffer, 0, charsRead);
                    log.Verbose(text.TrimEnd());
                }
            });

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new CommandException($"Claude Code exited with code {process.ExitCode}");
            }

            if (File.Exists(debugLogPath))
            {
                var fileInfo = new FileInfo(debugLogPath);     
                log.NewOctopusArtifact(debugLogPath, "claude-agent-debug.log", fileInfo.Length);
            }

            return responseBuilder.ToString();
        }

        internal static StringBuilder BuildArguments(ClaudeCodeOptions options, string workingDir)
        {
            // https://code.claude.com/docs/en/cli-reference
            var args = new StringBuilder();
            args.Append("-p ");
            args.Append(EscapeArg(options.Prompt));
            args.Append(" --model ");
            args.Append(EscapeArg(options.Model));
            args.Append(" --output-format stream-json");
            args.Append(" --verbose");
            args.Append(" --permission-mode dontAsk");
            args.Append(" --no-session-persistence");

            // MCP isolation: only use servers we explicitly provide
            var mcpConfigPath = Path.Combine(workingDir, "mcp-config.json");
            args.Append(" --strict-mcp-config");
            args.Append(" --mcp-config ");
            args.Append(EscapeArg(mcpConfigPath));

            // Tool whitelist
            if (options.AllowedTools.Count > 0)
            {
                args.Append(" --allowedTools ");
                args.Append(string.Join(",", options.AllowedTools));
            }

            if (options.MaxTurns.HasValue)
                args.Append($" --max-turns {options.MaxTurns.Value}");

            if (!string.IsNullOrWhiteSpace(options.SystemPrompt))
            {
                args.Append(" --system-prompt ");
                args.Append(EscapeArg(options.SystemPrompt));
            }

            return args;
        }

        internal static void SetupMcpConfig(string workingDir, IReadOnlyDictionary<string, McpServerConfig> mcpServers)
        {
            var config = new { mcpServers };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(workingDir, "mcp-config.json"), json);
        }

        internal static void SetupSkills(string workingDir)
        {
            var skillsDir = Path.Combine(workingDir, ".claude", "skills");
            Directory.CreateDirectory(skillsDir);

            File.WriteAllText(Path.Combine(skillsDir, "octopus-deployment-context.md"),
                """
                ---
                name: octopus-deployment-context
                description: Use when you need to understand the Octopus Deploy deployment context, including environment, project, tenant, release version, or any custom variables available during this deployment.
                ---

                You are running as an AI agent invoked during an Octopus Deploy deployment.

                Key context:
                - You are executing inside a deployment step on a target machine
                - Octopus deployment variables are available via the `get_deployment_variables` tool
                - Sensitive variables (passwords, tokens, API keys) are filtered out for safety
                - Your output will be captured as the step result

                When asked about the deployment context, always call `get_deployment_variables` first to get the actual values rather than guessing.
                """);
        }

        internal static void ApplyCredentials(ProcessStartInfo startInfo, ProcessCredentials credentials)
        {
            // See ADR: https://github.com/OctopusDeploy/adr/blob/main/team-modern-deployments/calamari-ai-agent/adr-001-use-processstartinfo-username-for-user-impersonation.md
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo.UserName = credentials.Username;
                if (!string.IsNullOrEmpty(credentials.Password))
                    startInfo.PasswordInClearText = credentials.Password;
                if (!string.IsNullOrEmpty(credentials.Domain))
                    startInfo.Domain = credentials.Domain;
            }
            else
            {
                // Wrap in: sudo -u <user> -- env KEY=VAL ... <command> <args>
                // We use env to pass required variables explicitly rather than sudo -E,
                // because -E requires SETENV permission in sudoers and can trigger a password
                // prompt even with NOPASSWD configured.
                // -- prevents claude's flags from being interpreted as sudo flags.
                var originalFileName = startInfo.FileName;
                var originalArgs = startInfo.Arguments;

                var envSection = new StringBuilder();
                foreach (var key in new[] { "ANTHROPIC_API_KEY"})
                {
                    if (startInfo.Environment.TryGetValue(key, out var value) && value != null)
                        envSection.Append($"{key}={EscapeArg(value)} ");
                }

                startInfo.FileName = "sudo";
                startInfo.Arguments = $"-u {credentials.Username} -- env {envSection}{originalFileName} {originalArgs}";
            }
        }

        static string EscapeArg(string arg)
        {
            if (arg.IndexOfAny(new[] { ' ', '"', '\\' }) < 0)
                return arg;

            return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }

    public record ClaudeCodeOptions
    {
        public required string Prompt { get; init; }
        public required string ApiToken { get; init; }
        public required string Model { get; init; }
        public string? SystemPrompt { get; init; }
        public int? MaxTurns { get; init; }
        public IReadOnlyList<string> AllowedTools { get; init; } = new[]
        {
            "Bash", "Read", "Write", "Edit", "Glob", "Grep", "WebSearch", "WebFetch"
        };
        public IReadOnlyDictionary<string, McpServerConfig> McpServers { get; init; } =
            new Dictionary<string, McpServerConfig>();
        public ProcessCredentials? RunAs { get; init; }
    }

    public record ProcessCredentials
    {
        public required string Username { get; init; }
        public string? Password { get; init; }
        public string? Domain { get; init; }
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
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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

        public async Task<string> RunAsync(
            ClaudeCommandArgsBuilder argsBuilder,
            string apiToken,
            IReadOnlyDictionary<string, McpServerConfig> mcpServers,
            ProcessCredentials? runAs = null,
            IReadOnlyList<UserSkill>? userSkills = null)
        {
            var workingDir = Path.Combine(Path.GetTempPath(), $"claude-agent-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workingDir);
            log.Verbose($"Claude Code working directory: {workingDir}");

            try
            {
                SetupSkills(workingDir, userSkills);
                SetupMcpConfig(workingDir, mcpServers);
                return await RunInDirectoryAsync(argsBuilder, apiToken, workingDir, runAs);
            }
            finally
            {
                try { Directory.Delete(workingDir, recursive: true); }
                catch { /* best effort cleanup */ }
            }
        }

        async Task<string> RunInDirectoryAsync(
            ClaudeCommandArgsBuilder argsBuilder,
            string apiToken,
            string workingDir,
            ProcessCredentials? runAs)
        {
            //var mcpConfigPath = Path.Combine(workingDir, "mcp-config.json");
            //var systemPromptPath = Path.Combine(workingDir, "CLAUDE.md");
            var verboseLogPath = Path.Combine(Path.GetTempPath(), $"claude-agent-verbose-{Guid.NewGuid():N}.log");
            var debugLogPath = Path.Combine(Path.GetTempPath(), $"claude-agent-debug-{Guid.NewGuid():N}.log");
            var fullArgs = argsBuilder.Build();
            //var fullArgs = $"{args} --strict-mcp-config " //"--bare "
                           //+ $"--system-prompt-file {EscapeArg(systemPromptPath)} "
                           fullArgs += $" --debug-file {EscapeArg(debugLogPath)}";
            
            log.Verbose($"Claude Code command: claude {fullArgs}");
            var startInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = fullArgs,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.Environment["ANTHROPIC_API_KEY"] = apiToken;

            if (runAs != null)
                ApplyCredentials(startInfo, runAs);

            var responseBuilder = new StringBuilder();
            var streamProcessor = new ClaudeCodeStreamProcessor(log, responseBuilder);

            try
            {
                await RunProcess(startInfo, verboseLogPath, streamProcessor);
            }
            catch (Exception e)
            {
                log.Error(e.Message);
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

            return responseBuilder.ToString();
        }

        async Task RunProcess(ProcessStartInfo startInfo, string verboseLogPath, ClaudeCodeStreamProcessor streamProcessor)
        {
            using var process = new Process();
            process.StartInfo = startInfo;
            process.Start();
            var stdoutTask = Task.Run(async () =>
                                      {
                                          while (await process.StandardOutput.ReadLineAsync() is { } line)
                                          {
                                              await File.AppendAllTextAsync(verboseLogPath, line);
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
        }

        internal static void SetupMcpConfig(string workingDir, IReadOnlyDictionary<string, McpServerConfig> mcpServers)
        {
            var config = new { mcpServers };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(workingDir, "mcp-config.json"), json);
        }

        const string SkillsResourcePrefix = "Calamari.AiAgent.ClaudeCodeBehaviour.DefaultContext.Skills.";
        const string SystemPromptResource = "Calamari.AiAgent.ClaudeCodeBehaviour.DefaultContext.system-prompt.md";

        internal static void SetupSkills(string workingDir, IReadOnlyList<UserSkill>? userSkills = null)
        {
            var skillsDir = Path.Combine(workingDir, ".claude", "skills");
            Directory.CreateDirectory(skillsDir);

            var assembly = Assembly.GetExecutingAssembly();

            // Write CLAUDE.md (system prompt) to the working directory root
            using (var promptStream = assembly.GetManifestResourceStream(SystemPromptResource))
            {
                if (promptStream != null)
                {
                    using var reader = new StreamReader(promptStream);
                    File.WriteAllText(Path.Combine(workingDir, "CLAUDE.md"), reader.ReadToEnd());
                }
            }

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(SkillsResourcePrefix, StringComparison.Ordinal))
                    continue;
                
                var fileName = resourceName.Substring(SkillsResourcePrefix.Length);
                var skillName = Path.GetFileNameWithoutExtension(fileName);
                var innerSkillDir = Path.Combine(skillsDir, skillName);

                using var stream = assembly.GetManifestResourceStream(resourceName)!;
                using var reader = new StreamReader(stream);

                Directory.CreateDirectory(innerSkillDir);
                File.WriteAllText(Path.Combine(innerSkillDir, "SKILL.md"), reader.ReadToEnd());
            }

            if (userSkills != null)
            {
                foreach (var skill in userSkills)
                {
                    var dirName = SanitizeFileName(skill.Name);
                    var innerSkillDir = Path.GetFullPath(Path.Combine(skillsDir, dirName));
                    
                    if (!innerSkillDir.StartsWith(Path.GetFullPath(skillsDir) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                        throw new CommandException($"Skill name '{skill.Name}' results in a path outside the skills directory.");

                    Directory.CreateDirectory(innerSkillDir);
                    File.WriteAllText(Path.Combine(innerSkillDir, "SKILL.md"), skill.Content);
                }
            }
        }

        static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        internal static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new CommandException("Skill name cannot be empty.");

            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new StringBuilder(name.Length);
            foreach (var c in name)
                sanitized.Append(Array.IndexOf(invalid, c) >= 0 ? '-' : c);

            // Strip leading dots to prevent hidden files / relative path tricks
            var result = sanitized.ToString().TrimStart('.');

            if (string.IsNullOrWhiteSpace(result))
                throw new CommandException($"Skill name '{name}' is not a valid file name.");

            if (WindowsReservedNames.Contains(result))
                throw new CommandException($"Skill name '{name}' is a reserved file name.");

            // Filesystem limits are typically 255 bytes; truncate to be safe
            if (result.Length > 200)
                result = result.Substring(0, 200);

            return result;
        }

        internal static void ApplyCredentials(ProcessStartInfo startInfo, ProcessCredentials credentials)
        {
            // See ADR: https://github.com/OctopusDeploy/adr/blob/main/team-modern-deployments/calamari-ai-agent/adr-001-use-processstartinfo-username-for-user-impersonation.md
            // Uses ProcessStartInfo.UserName on all platforms.
            // On Windows: uses native token-based impersonation with optional password/domain.
            // On Linux: .NET calls setuid/setgid internally. Requires the calling process to
            // be root or have CAP_SETUID/CAP_SETGID capabilities. Environment variables from
            // ProcessStartInfo.Environment are inherited naturally — no special handling needed.
            startInfo.UserName = credentials.Username;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!string.IsNullOrEmpty(credentials.Password))
                    startInfo.PasswordInClearText = credentials.Password;
                if (!string.IsNullOrEmpty(credentials.Domain))
                    startInfo.Domain = credentials.Domain;
            }
        }

        static string EscapeArg(string arg)
        {
            if (arg.IndexOfAny(new[] { ' ', '"', '\\' }) < 0)
                return arg;

            return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
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
}

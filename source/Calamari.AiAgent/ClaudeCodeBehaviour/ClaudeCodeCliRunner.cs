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
            IReadOnlyDictionary<string, string> deploymentVariables,
            ProcessCredentials? runAs = null,
            IReadOnlyList<UserSkill>? userSkills = null)
        {
            var workingDir = Path.Combine(Path.GetTempPath(), $"claude-agent-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workingDir);
            log.Verbose($"Claude Code working directory: {workingDir}");

            try
            {
                SetupSkills(workingDir, userSkills);
                SetupDeploymentVariables(workingDir, deploymentVariables);

                var systemPromptPath = SetupSystemPrompt(workingDir);
                argsBuilder.WithAppendSystemPromptFile(systemPromptPath);

                var mcpConfigPath = SetupMcpConfig(workingDir, mcpServers);
                argsBuilder.WithMcpConfigPath(mcpConfigPath);

                return await RunInDirectoryAsync(argsBuilder, apiToken, workingDir, runAs);
            }
            finally
            {
                try { Directory.Delete(workingDir, recursive: true); }
                catch { /* best effort cleanup */ }
            }
        }
        
        /*
         using System.Diagnostics;
           
           string user = "claude";
           string cmd = "bash";
           string password = "claude";
           
           var psi = new ProcessStartInfo {
               FileName = "script",
               ArgumentList = { "-qec", $"su - {user} -c '{cmd}'", "/dev/null" },
               RedirectStandardInput  = true,
               RedirectStandardOutput = true,
               RedirectStandardError  = true,
               UseShellExecute = false,
           };
           
           using var p = Process.Start(psi)!;
           p.StandardInput.WriteLine(password);
           
           
           
           p.WaitForExit();
           Console.WriteLine($"exit={p.ExitCode}\n{output}");
         
         */

        async Task<string> RunInDirectoryAsync(
            ClaudeCommandArgsBuilder argsBuilder,
            string apiToken,
            string workingDir,
            ProcessCredentials? runAs)
        {
            var verboseLogPath = Path.Combine(Path.GetTempPath(), $"claude-agent-verbose-{Guid.NewGuid():N}.log");
            var debugLogPath = Path.Combine(Path.GetTempPath(), $"claude-agent-debug-{Guid.NewGuid():N}.log");
            var fullArgs = argsBuilder.Build();
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

            var customEnvVars = new Dictionary<string, string>
            {
                ["ANTHROPIC_API_KEY"] = apiToken,
            };

            foreach (var kvp in customEnvVars)
                startInfo.Environment[kvp.Key] = kvp.Value;

            if (runAs != null)
                ApplyCredentials(startInfo, runAs, customEnvVars);

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
                                              await File.AppendAllTextAsync(verboseLogPath, line + "\n");
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

        internal static string SetupMcpConfig(string workingDir, IReadOnlyDictionary<string, McpServerConfig> mcpServers)
        {
            var config = new { mcpServers };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            var path = Path.Combine(workingDir, "mcp-config.json");
            File.WriteAllText(path, json);
            return path;
        }

        const string SkillsResourcePrefix = "Calamari.AiAgent.ClaudeCodeBehaviour.DefaultContext.Skills.";
        const string SystemPromptResource = "Calamari.AiAgent.ClaudeCodeBehaviour.DefaultContext.system-prompt.md";

        internal static string SetupSystemPrompt(string workingDir)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var path = Path.Combine(workingDir, "system-prompt.md");

            using var stream = assembly.GetManifestResourceStream(SystemPromptResource);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                File.WriteAllText(path, reader.ReadToEnd());
            }

            return path;
        }

        internal static void SetupDeploymentVariables(string workingDir, IReadOnlyDictionary<string, string> variables)
        {
            var json = JsonSerializer.Serialize(variables, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(workingDir, "deployment-variables.json"), json);
        }

        internal static void SetupSkills(string workingDir, IReadOnlyList<UserSkill>? userSkills = null)
        {
            var skillsDir = Path.Combine(workingDir, ".claude", "skills");
            Directory.CreateDirectory(skillsDir);

            var assembly = Assembly.GetExecutingAssembly();

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

        internal static void ApplyCredentials(ProcessStartInfo startInfo, ProcessCredentials credentials, Dictionary<string, string> customEnvVars)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo.UserName = credentials.Username;

                if (!string.IsNullOrEmpty(credentials.Password))
                    startInfo.PasswordInClearText = credentials.Password;
                if (!string.IsNullOrEmpty(credentials.Domain))
                    startInfo.Domain = credentials.Domain;

                return;
            }

            // Linux: use script/su to impersonate the user with a proper login shell.
            // su - starts a login shell which clears the environment, so we inline
            // any custom env vars into the command string.
            if (string.IsNullOrEmpty(credentials.Password))
                throw new CommandException("A password is required for Linux user impersonation via su");

            var envPrefix = string.Join(" ", customEnvVars.Select(kvp => $"{kvp.Key}={ShellQuote(kvp.Value)}"));
            var innerCommand = string.IsNullOrEmpty(envPrefix)
                ? $"{startInfo.FileName} {startInfo.Arguments}"
                : $"{envPrefix} {startInfo.FileName} {startInfo.Arguments}";

            var suCommand = $"su - {credentials.Username} -c {ShellQuote(innerCommand)}";

            startInfo.FileName = "script";
            startInfo.Arguments = ""; // clear — using ArgumentList instead
            startInfo.ArgumentList.Add("-qec");
            startInfo.ArgumentList.Add(suCommand);
            startInfo.ArgumentList.Add("/dev/null");
            startInfo.RedirectStandardInput = true;
            startInfo.UserName = null;
        }

        internal static string ShellQuote(string value)
        {
            return "'" + value.Replace("'", @"'\''") + "'";
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.Behaviours
{
    public class InvokeClaudeCodeBehaviour : IDeployBehaviour
    {
        readonly ILog log;

        public InvokeClaudeCodeBehaviour(ILog log)
        {
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
            //var provider = context.Variables.Get(SpecialVariables.Action.AiAgent.Provider);
            //return provider == "ClaudeCode";
        }

        public async Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;

            var prompt = variables.Get(SpecialVariables.Action.AiAgent.Prompt);
            if (string.IsNullOrWhiteSpace(prompt))
                throw new CommandException($"Variable '{SpecialVariables.Action.AiAgent.Prompt}' is required but was not provided.");

            var apiToken = variables.Get(SpecialVariables.Action.AiAgent.ApiToken);
            if (string.IsNullOrWhiteSpace(apiToken))
                throw new CommandException($"Variable '{SpecialVariables.Action.AiAgent.ApiToken}' is required but was not provided.");

            var model = variables.Get(SpecialVariables.Action.AiAgent.Model);
            if (string.IsNullOrWhiteSpace(model))
                model = "claude-sonnet-4-20250514";

            log.Info($"Invoking Claude Code CLI with model '{model}'...");

            var mcpServers = BuildMcpServers(variables);
            var runAs = BuildRunAs(variables);

            var defaultAllowedTools = new[] { "Bash", "Read", "Write", "Edit", "Glob", "Grep", "WebSearch", "WebFetch" };
            var allowedToolsRaw = variables.Get(SpecialVariables.Action.AiAgent.AllowedTools);
            var allowedTools = new List<string>(!string.IsNullOrWhiteSpace(allowedToolsRaw)
                ? allowedToolsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : defaultAllowedTools);

            // Auto-allow all tools from configured MCP servers
            foreach (var serverName in mcpServers.Keys)
                allowedTools.Add($"mcp__{serverName}__*");

            var argsBuilder = new ClaudeCommandArgsBuilder()
                .WithPrompt(prompt)
                .WithModel(model)
                .WithAllowedTools(allowedTools);

            var systemPrompt = variables.Get(SpecialVariables.Action.AiAgent.SystemSkill);
            if (!string.IsNullOrWhiteSpace(systemPrompt))
                argsBuilder.WithSystemPrompt(systemPrompt);

            var maxTurns = variables.GetInt32(SpecialVariables.Action.AiAgent.MaxTurns);
            if (maxTurns.HasValue)
                argsBuilder.WithMaxTurns(maxTurns.Value);

            var maxBudgetUsdRaw = variables.Get(SpecialVariables.Action.AiAgent.MaxBudgetUsd);
            if (!string.IsNullOrWhiteSpace(maxBudgetUsdRaw)
                && decimal.TryParse(maxBudgetUsdRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var budgetUsd))
                argsBuilder.WithMaxBudgetUsd(budgetUsd);

            var effort = variables.Get(SpecialVariables.Action.AiAgent.Effort);
            if (!string.IsNullOrWhiteSpace(effort))
                argsBuilder.WithEffort(effort);

            var userSkills = BuildUserSkills(variables);
            var deploymentVariables = BuildDeploymentVariables(variables);

            var runner = new ClaudeCodeCliRunner(log);
            var response = await runner.RunAsync(argsBuilder, apiToken, mcpServers, deploymentVariables, runAs, userSkills);

            Log.SetOutputVariable(SpecialVariables.Action.AiAgent.Response, response, variables);
            log.Info("Claude Code invocation complete.");
        }

        static Dictionary<string, McpServerConfig> BuildMcpServers(IVariables variables)
        {
            var servers = new Dictionary<string, McpServerConfig>();
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";

            // Octopus MCP server is always added when a token is available
            var octopusToken = variables.Get(SpecialVariables.Action.AiAgent.OctopusToken);
            if (!string.IsNullOrWhiteSpace(octopusToken))
            {
                var octopusServerUrl = variables.Get("Octopus.Web.ServerUri");
                if (string.IsNullOrWhiteSpace(octopusServerUrl))
                {
                    Log.Warn("Unable to find Octopus Server URL");
                }
                else
                {
                    Log.Verbose("Octopus Server URL: " + octopusServerUrl);
                    servers["octopus"] = new McpServerConfig
                    {
                        Command = "npx",
                        Args = new[] { "-y", "@octopusdeploy/mcp-server" },
                        Env = new Dictionary<string, string>
                        {
                            ["OCTOPUS_SERVER_URL"] = octopusServerUrl,
                            ["OCTOPUS_API_KEY"] = octopusToken,
                            ["PATH"] = path,
                        },
                    };
                }
            }

            // User-configured MCP servers from JSON variable
            var mcpServersJson = variables.Get(SpecialVariables.Action.AiAgent.McpServers);
            if (!string.IsNullOrWhiteSpace(mcpServersJson))
            {
                List<McpServerEntry>? entries;
                try
                {
                    entries = JsonSerializer.Deserialize<List<McpServerEntry>>(mcpServersJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    throw new CommandException($"Failed to parse MCP servers configuration: {ex.Message}");
                }

                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Name))
                            throw new CommandException("Each MCP server must have a name.");
                        if (string.IsNullOrWhiteSpace(entry.Command))
                            throw new CommandException($"MCP server '{entry.Name}' must have a command.");

                        var env = entry.Env != null
                            ? new Dictionary<string, string>(entry.Env)
                            : new Dictionary<string, string>();

                        if (!env.ContainsKey("PATH"))
                            env["PATH"] = path;

                        servers[entry.Name] = new McpServerConfig
                        {
                            Type = entry.Type ?? "stdio",
                            Command = entry.Command,
                            Args = entry.Args,
                            Env = env,
                        };
                        Log.Verbose($"MCP server '{entry.Name}' added.");
                    }
                }
            }

            return servers;
        }

        static List<UserSkill> BuildUserSkills(IVariables variables)
        {
            var skills = new List<UserSkill>();
            var indexes = variables.GetIndexes(SpecialVariables.Action.AiAgent.Skills);
            foreach (var index in indexes)
            {
                var prefix = $"{SpecialVariables.Action.AiAgent.Skills}[{index}].";
                var name = variables.Get(prefix + SpecialVariables.Action.AiAgent.SkillName);
                var content = variables.Get(prefix + SpecialVariables.Action.AiAgent.SkillContent);

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(content))
                    skills.Add(new UserSkill { Name = name, Content = content });
            }
            return skills;
        }

        static ProcessCredentials? BuildRunAs(IVariables variables)
        {
            var username = variables.Get(SpecialVariables.Action.AiAgent.RunAsUsername);
            if (string.IsNullOrWhiteSpace(username))
                return null;

            return new ProcessCredentials
            {
                Username = username,
                Password = variables.Get(SpecialVariables.Action.AiAgent.RunAsPassword),
            };
        }

        static readonly string[] SensitiveKeywords = { "password", "secret", "token", "apikey", "api_key", "api-key", "private" };

        static Dictionary<string, string> BuildDeploymentVariables(IVariables variables)
        {
            return variables
                .Where(kvp => !SensitiveKeywords.Any(k => kvp.Key.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class McpWriter(IVariables variables)
{
    static readonly string ConfigName = "mcp-config.json";
    
    public McpConfigResult SetupMcpConfig(string workingDir)
    {
        // Secret env values are referenced from mcp-config.json as ${VAR} placeholders and
        // passed to the claude process env instead of being written to disk in plaintext.
        // Claude expands ${VAR} in stdio server env values from its own process env at launch.
        var secretEnvVars = new Dictionary<string, string>();
        var mcpServers = BuildMcpServers(secretEnvVars);
        var config = new { mcpServers };
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(workingDir, ConfigName);
        File.WriteAllText(path, json);
        return new McpConfigResult(path, secretEnvVars);
    }

    public IEnumerable<string> GetAllowedTools()
    {
        var mcpServers = GetCustomMcpServers();
            
        // TODO: Use explicitly allowed MCP tools
        return mcpServers.Select(serverName => $"mcp__{serverName.Name}__*");
    }
    
    Dictionary<string, McpServerConfig> BuildMcpServers(Dictionary<string, string> secretEnvVars)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";

        var servers = AddCustomMcpServer(path, secretEnvVars);
        AddOctopusMcp(servers, path, secretEnvVars);
        return servers;
    }

    void AddOctopusMcp(Dictionary<string, McpServerConfig> servers, string path, Dictionary<string, string> secretEnvVars)
    {
        var octopusToken = variables.Get(SpecialVariables.Action.Claude.OctopusToken);
        if (string.IsNullOrWhiteSpace(octopusToken))
            return;


        // Octopus MCP server is always added when a token is available
        var octopusServerUrl = variables.Get(SpecialVariables.Web.ServerUri);
        if (string.IsNullOrWhiteSpace(octopusServerUrl))
        {
            Log.Warn("Unable to find Octopus Server URL");
        }
        else
        {
            Log.Verbose("Octopus Server URL: " + octopusServerUrl);
            secretEnvVars["OCTOPUS_API_KEY"] = octopusToken;
            servers["octopus"] = new McpServerConfig
            {
                Command = "npx",
                Args = new[] { "-y", "@octopusdeploy/mcp-server" },
                Env = new Dictionary<string, string>
                {
                    ["OCTOPUS_SERVER_URL"] = octopusServerUrl,
                    ["OCTOPUS_API_KEY"] = "${OCTOPUS_API_KEY}",
                    ["PATH"] = path,
                },
            };
        }
    }

    List<McpServerEntry> GetCustomMcpServers()
    {
        var mcpServersJson = variables.Get(SpecialVariables.Action.Claude.McpServers);
        if (string.IsNullOrWhiteSpace(mcpServersJson))
        {
            return new List<McpServerEntry>();
        }
        
        try
        {
            var customServers = JsonSerializer.Deserialize<List<McpServerEntry>>(mcpServersJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return customServers ?? new List<McpServerEntry>();
        }
        catch (JsonException ex)
        {
            throw new CommandException($"Failed to parse MCP servers configuration: {ex.Message}");
        }
    }

     Dictionary<string, McpServerConfig> AddCustomMcpServer(string path, Dictionary<string, string> secretEnvVars)
    {
        var entries = GetCustomMcpServers();

        var mcpServerConfigs = new Dictionary<string, McpServerConfig>();
        if (entries.Any())
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    throw new CommandException("Each MCP server must have a name.");
                if (string.IsNullOrWhiteSpace(entry.Command))
                    throw new CommandException($"MCP server '{entry.Name}' must have a command.");

                var env = new Dictionary<string, string>();
                if (entry.Env != null)
                {
                    foreach (var kvp in entry.Env)
                    {
                        // PATH is not a secret and is needed verbatim to locate the server command.
                        if (kvp.Key == "PATH")
                        {
                            env[kvp.Key] = kvp.Value;
                            continue;
                        }

                        // Custom server env values may be secrets, so reference them as ${VAR}
                        // placeholders and pass the real values via the claude process env.
                        var placeholder = ReserveEnvVarName(secretEnvVars, entry.Name, kvp.Key);
                        secretEnvVars[placeholder] = kvp.Value;
                        env[kvp.Key] = $"${{{placeholder}}}";
                    }
                }

                if (!env.ContainsKey("PATH"))
                    env["PATH"] = path;

                mcpServerConfigs[entry.Name] = new McpServerConfig
                {
                    Type = entry.Type ?? "stdio",
                    Command = entry.Command,
                    Args = entry.Args,
                    Env = env,
                };
                Log.Verbose($"MCP server '{entry.Name}' added.");
            }
        }

        return mcpServerConfigs;
    }

    // Builds a deterministic, collision-safe env var name for a custom server's env entry.
    static string ReserveEnvVarName(Dictionary<string, string> secretEnvVars, string serverName, string key)
    {
        var raw = $"MCP_{serverName}_{key}".ToUpperInvariant();
        var chars = raw.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        var name = new string(chars);
        if (name.Length == 0 || char.IsDigit(name[0]))
            name = "_" + name;

        var candidate = name;
        var suffix = 1;
        while (secretEnvVars.ContainsKey(candidate))
            candidate = $"{name}_{suffix++}";

        return candidate;
    }
}

public record McpConfigResult(string Path, IReadOnlyDictionary<string, string> SecretEnvVars);

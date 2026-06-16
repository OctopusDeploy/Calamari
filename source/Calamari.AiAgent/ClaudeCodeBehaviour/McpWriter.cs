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
    
    public string SetupMcpConfig(string workingDir)
    {
        var mcpServers = BuildMcpServers();
        var config = new { mcpServers };
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(workingDir, ConfigName);
        File.WriteAllText(path, json);
        return path;
    }

    public IEnumerable<string> GetAllowedTools()
    {
        var mcpServers = GetCustomMcpServers();
            
        // TODO: Use explicitly allowed MCP tools
        return mcpServers.Select(serverName => $"mcp__{serverName.Name}__*");
    }
    
    Dictionary<string, McpServerConfig> BuildMcpServers()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        
        var servers = AddCustomMcpServer(path);
        AddOctopusMcp(servers, path);
        return servers;
    }

    void AddOctopusMcp(Dictionary<string, McpServerConfig> servers, string path)
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

     Dictionary<string, McpServerConfig> AddCustomMcpServer(string path)
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

                var env = entry.Env != null
                    ? new Dictionary<string, string>(entry.Env)
                    : new Dictionary<string, string>();

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


        

}
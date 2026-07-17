using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Octopus.Calamari.Contracts.ClaudeCode;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class McpWriter(IVariables variables)
{
    const string ConfigName = "mcp-config.json";
    const string OctopusServerName = "octopus";

    static readonly JsonSerializerOptions VariableJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // Must be set for our type discriminator to work
        AllowOutOfOrderMetadataProperties = true,
    };
    static readonly JsonSerializerOptions ConfigJsonOptions = new() { WriteIndented = true };

    public string SetupMcpConfig(string workingDir)
    {
        var mcpServers = LoadServers().ToDictionary(server => server.Name, ToClaudeConfig);
        var json = JsonSerializer.Serialize(new { mcpServers }, ConfigJsonOptions);
        var path = Path.Combine(workingDir, ConfigName);
        File.WriteAllText(path, json);
        return path;
    }

    public IEnumerable<string> GetAllowedTools()
        => LoadServers().SelectMany(server => server.AllowedTools.Select(tool => $"mcp__{server.Name}__{tool}"));

    IReadOnlyList<McpServer> LoadServers()
    {
        var allServers = GetConfiguredServers().ToList();
        if (BuildOctopusServer() is { } octopus)
            allServers.Add(octopus);

        foreach (var server in allServers)
            Validate(server);

        var duplicateNames = allServers.GroupBy(server => server.Name)
                                .Where(group => group.Count() > 1)
                                .Select(group => group.Key)
                                .ToArray();
        if (duplicateNames.Length > 0)
            throw new CommandException($"Duplicate MCP server names: {string.Join(", ", duplicateNames)}.");

        return allServers;
    }

    IReadOnlyList<McpServer> GetConfiguredServers()
    {
        var json = variables.Get(SpecialVariables.Action.Claude.McpServers);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<McpServer[]>(json, VariableJsonOptions) ?? [];
        }
        
        // a payload with a misconfigured `type` discriminator throws a NotSupportedException, so we catch them here as well
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new CommandException($"Failed to parse MCP servers configuration: {ex.Message}");
        }
    }

    // The Octopus MCP server is always added when an API key is available
    StdioMcpServer? BuildOctopusServer()
    {
        var apiKey = variables.Get(SpecialVariables.Action.Claude.OctopusMcpApiKey);
        // fall back to the legacy OctopusToken
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = variables.Get(SpecialVariables.Action.Claude.OctopusToken);
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var octopusServerUrl = variables.Get(SpecialVariables.Web.ServerUri);
        if (string.IsNullOrWhiteSpace(octopusServerUrl))
        {
            Log.Warn("Unable to find Octopus Server URL");
            return null;
        }

        Log.Verbose("Octopus Server URL: " + octopusServerUrl);
        return new StdioMcpServer
        {
            Name = OctopusServerName,
            Command = "npx",
            Args = ["-y", "@octopusdeploy/mcp-server"],
            Env = new Dictionary<string, string>
            {
                ["OCTOPUS_SERVER_URL"] = octopusServerUrl,
                ["OCTOPUS_API_KEY"] = apiKey,
            },
            AllowedTools = GetOctopusMcpTools(),
        };
    }

    // When no tools are configured, we deny all tools rather than allowing everything
    IReadOnlyCollection<string> GetOctopusMcpTools()
    {
        var json = variables.Get(SpecialVariables.Action.Claude.OctopusMcpTools);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        string[]? tools;
        try
        {
            tools = JsonSerializer.Deserialize<string[]>(json, VariableJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new CommandException($"Failed to parse Octopus MCP tools configuration: {ex.Message}");
        }

        return tools ?? [];
    }

    static void Validate(McpServer server)
    {
        if (string.IsNullOrWhiteSpace(server.Name))
            throw new CommandException("Each MCP server must have a name.");

        switch (server)
        {
            case StdioMcpServer stdio when string.IsNullOrWhiteSpace(stdio.Command):
                throw new CommandException($"MCP server '{server.Name}' must have a command.");
            case HttpMcpServer http when string.IsNullOrWhiteSpace(http.Url):
                throw new CommandException($"MCP server '{server.Name}' must have a URL.");
        }
    }

    static object ToClaudeConfig(McpServer server) => server switch
    {
        StdioMcpServer stdio => new ClaudeStdioServerConfig
        {
            Command = stdio.Command,
            Args = stdio.Args,
            Env = WithWorkerPath(stdio.Env),
        },
        HttpMcpServer http => new ClaudeHttpServerConfig
        {
            Url = http.Url,
            Headers = http.Headers,
        },
        _ => throw new CommandException($"MCP server '{server.Name}' has unsupported type '{server.GetType().Name}'."),
    };

    // stdio servers are spawned without a profile, so commands like npx only resolve with the worker's PATH
    static IReadOnlyDictionary<string, string> WithWorkerPath(IReadOnlyDictionary<string, string> env)
    {
        if (env.ContainsKey("PATH"))
            return env;

        return new Dictionary<string, string>(env)
        {
            ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "",
        };
    }
}

public record ClaudeStdioServerConfig
{
    [JsonPropertyName("type")]
    public string Type => StdioMcpServer.DiscriminatorValue;

    [JsonPropertyName("command")]
    public required string Command { get; init; }

    [JsonPropertyName("args")]
    public required IReadOnlyList<string> Args { get; init; }

    [JsonPropertyName("env")]
    public required IReadOnlyDictionary<string, string> Env { get; init; }
}

public record ClaudeHttpServerConfig
{
    [JsonPropertyName("type")]
    public string Type => HttpMcpServer.DiscriminatorValue;

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("headers")]
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}

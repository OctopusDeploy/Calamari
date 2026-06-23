using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

/// <summary>
/// Prepares MCP configuration for a Claude Code run. Every MCP server (the Octopus server and any
/// user-configured custom servers) now runs as a Calamari child behind the credential broker
/// (see <see cref="McpBroker"/>), so this no longer writes any secret to disk. It parses the
/// configured servers into <see cref="McpServerSpec"/>s for the broker to spawn, and writes a
/// secret-free mcp-config.json that points Claude Code at the broker's loopback HTTP endpoints.
/// </summary>
public class McpWriter(IVariables variables)
{
    const string ConfigName = "mcp-config.json";

    /// <summary>
    /// The set of MCP servers to front: the Octopus server (when a token + URL are available) plus any
    /// user-configured custom servers. The broker spawns each as a Calamari child holding its secrets.
    /// </summary>
    public IReadOnlyList<McpServerSpec> BuildServerSpecs()
    {
        var specs = new List<McpServerSpec>();
        specs.AddRange(BuildCustomServerSpecs());

        var octopus = BuildOctopusServerSpec();
        if (octopus != null)
            specs.Add(octopus);

        return specs;
    }

    /// <summary>
    /// Writes a secret-free mcp-config.json mapping each brokered server to its loopback HTTP endpoint.
    /// </summary>
    public string WriteConfig(string workingDir, IReadOnlyDictionary<string, Uri> brokerEndpoints)
    {
        var mcpServers = brokerEndpoints.ToDictionary(
            endpoint => endpoint.Key,
            endpoint => new McpConfigEntry { Url = endpoint.Value.ToString() });

        var config = new { mcpServers };
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(workingDir, ConfigName);
        File.WriteAllText(path, json);
        return path;
    }

    // TODO: Use explicitly allowed MCP tools rather than a per-server wildcard.
    public IEnumerable<string> GetAllowedTools(IReadOnlyList<McpServerSpec> specs)
        => specs.Select(spec => $"mcp__{spec.Name}__*");

    McpServerSpec? BuildOctopusServerSpec()
    {
        var octopusToken = variables.Get(SpecialVariables.Action.Claude.OctopusToken);
        if (string.IsNullOrWhiteSpace(octopusToken))
            return null;

        var octopusServerUrl = variables.Get(SpecialVariables.Web.ServerUri);
        if (string.IsNullOrWhiteSpace(octopusServerUrl))
        {
            Log.Warn("Unable to find Octopus Server URL; the Octopus MCP server will not be available.");
            return null;
        }

        Log.Verbose("Octopus Server URL: " + octopusServerUrl);
        return new McpServerSpec
        {
            Name = "octopus",
            Command = "npx",
            Args = new[] { "-y", "@octopusdeploy/mcp-server" },
            Env = new Dictionary<string, string?>
            {
                ["OCTOPUS_SERVER_URL"] = octopusServerUrl,
                ["OCTOPUS_API_KEY"] = octopusToken,
            },
        };
    }

    IReadOnlyList<McpServerSpec> BuildCustomServerSpecs()
    {
        var mcpServersJson = variables.Get(SpecialVariables.Action.Claude.McpServers);
        if (string.IsNullOrWhiteSpace(mcpServersJson))
            return Array.Empty<McpServerSpec>();

        List<CustomMcpServerJson>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<CustomMcpServerJson>>(mcpServersJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new CommandException($"Failed to parse MCP servers configuration: {ex.Message}");
        }

        if (entries == null)
            return Array.Empty<McpServerSpec>();

        var specs = new List<McpServerSpec>();
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                throw new CommandException("Each MCP server must have a name.");
            if (string.IsNullOrWhiteSpace(entry.Command))
                throw new CommandException($"MCP server '{entry.Name}' must have a command.");

            Log.Verbose($"MCP server '{entry.Name}' added.");
            specs.Add(new McpServerSpec
            {
                Name = entry.Name,
                Command = entry.Command,
                Args = entry.Args,
                Env = entry.Env?.ToDictionary(kv => kv.Key, kv => (string?)kv.Value),
            });
        }

        return specs;
    }
}

/// <summary>The raw, untrusted shape of one entry in the McpServers variable; validated into an <see cref="McpServerSpec"/>.</summary>
file record CustomMcpServerJson
{
    public string? Name { get; init; }
    public string? Command { get; init; }
    public IReadOnlyList<string>? Args { get; init; }
    public IReadOnlyDictionary<string, string>? Env { get; init; }
}

/// <summary>One entry written to the agent's mcp-config.json — always the broker's loopback HTTP endpoint.</summary>
file record McpConfigEntry
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "http";

    [JsonPropertyName("url")]
    public required string Url { get; init; }
}

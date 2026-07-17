using System.Text.Json;
using System.Text.Json.Nodes;
using Calamari.Common.Commands;
using ClaudeVariables = Calamari.AiAgent.SpecialVariables.Action.Claude;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class BashSandboxSettings(string? sandboxSettingsJson) : IClaudeSettingsJson
{
    public JsonObject Build()
    {
        if (string.IsNullOrWhiteSpace(sandboxSettingsJson))
            throw new CommandException($"Sandbox settings are required when a sandbox mode is selected. Expected '{ClaudeVariables.SandboxSettings}' to contain the sandbox settings JSON.");

        try
        {
            return JsonNode.Parse(sandboxSettingsJson) as JsonObject
                   ?? throw new CommandException($"'{ClaudeVariables.SandboxSettings}' must be a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new CommandException($"Failed to parse '{ClaudeVariables.SandboxSettings}' as JSON: {ex.Message}");
        }
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Calamari.Common.Commands;
using ClaudeVariables = Calamari.AiAgent.SpecialVariables.Action.Claude;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class CommandPermissionsSettings(string permissionsJson) : IClaudeSettingsJson
{
    public JsonObject Build()
    {
        try
        {
            return new JsonObject { ["permissions"] = JsonNode.Parse(permissionsJson) };
        }
        catch (JsonException ex)
        {
            throw new CommandException($"Failed to parse '{ClaudeVariables.Permissions}' as JSON: {ex.Message}");
        }
    }
}

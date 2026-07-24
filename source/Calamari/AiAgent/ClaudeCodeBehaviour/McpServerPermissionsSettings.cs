using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class McpServerPermissionsSettings(IReadOnlyList<string> allowedTools) : IClaudeSettingsJson
{
    public JsonObject Build()
    {
        var allow = new JsonArray();
        foreach (var tool in allowedTools)
            allow.Add(tool);

        return new JsonObject { ["permissions"] = new JsonObject { ["allow"] = allow } };
    }
}

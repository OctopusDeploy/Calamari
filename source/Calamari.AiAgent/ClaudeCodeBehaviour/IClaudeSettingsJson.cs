using System.Text.Json.Nodes;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public interface IClaudeSettingsJson
{
    JsonObject Build();
}

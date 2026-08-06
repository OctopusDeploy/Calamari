using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;

[JsonConverter(typeof(JsonStringEnumConverter<ContentBlockType>))]
public enum ContentBlockType
{
    [EnumMember(Value = "text")]
    Text,

    [EnumMember(Value = "thinking")]
    Thinking,

    [EnumMember(Value = "redacted_thinking")]
    RedactedThinking,

    [EnumMember(Value = "tool_use")]
    ToolUse,

    [EnumMember(Value = "tool_result")]
    ToolResult,

    [EnumMember(Value = "server_tool_use")]
    ServerToolUse,

    [EnumMember(Value = "server_tool_result")]
    ServerToolResult
}

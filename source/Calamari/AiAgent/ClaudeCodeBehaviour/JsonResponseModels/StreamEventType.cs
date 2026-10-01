using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;

[JsonConverter(typeof(JsonStringEnumConverter<StreamEventType>))]
public enum StreamEventType
{
    [EnumMember(Value = "system")]
    System,

    [EnumMember(Value = "assistant")]
    Assistant,

    [EnumMember(Value = "user")]
    User,

    [EnumMember(Value = "result")]
    Result
}

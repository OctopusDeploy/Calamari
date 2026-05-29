using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.Behaviours
{
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

    public record StreamEvent
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("session_id")]
        public string? SessionId { get; init; }
    }

    public record SystemStreamEvent : StreamEvent
    {
        [JsonPropertyName("subtype")]
        public string? Subtype { get; init; }

        [JsonPropertyName("attempt")]
        public int? Attempt { get; init; }

        [JsonPropertyName("retry_delay_ms")]
        public int? RetryDelayMs { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("error_status")]
        public int? ErrorStatus { get; init; }
    }

    public record AssistantStreamEvent : StreamEvent
    {
        [JsonPropertyName("message")]
        public StreamMessage? Message { get; init; }
    }

    public record UserStreamEvent : StreamEvent
    {
        [JsonPropertyName("message")]
        public StreamMessage? Message { get; init; }
    }

    public record ResultStreamEvent : StreamEvent
    {
        [JsonPropertyName("result")]
        public string? Result { get; init; }

        [JsonPropertyName("cost_usd")]
        public double? CostUsd { get; init; }

        [JsonPropertyName("total_cost_usd")]
        public double? TotalCostUsd { get; init; }

        [JsonPropertyName("duration_ms")]
        public double? DurationMs { get; init; }

        [JsonPropertyName("duration_api_ms")]
        public double? DurationApiMs { get; init; }

        [JsonPropertyName("num_turns")]
        public int? NumTurns { get; init; }

        [JsonPropertyName("usage")]
        public UsageInfo? Usage { get; init; }
    }

    public record StreamMessage
    {
        [JsonPropertyName("content")]
        public JsonElement[]? Content { get; init; }
    }

    public record ContentBlock
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }
    }

    public record TextContentBlock : ContentBlock
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    public record ThinkingContentBlock : ContentBlock
    {
        [JsonPropertyName("thinking")]
        public string? Thinking { get; init; }
    }

    public record RedactedThinkingContentBlock : ContentBlock;

    public record ToolUseContentBlock : ContentBlock
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("input")]
        public JsonElement? Input { get; init; }
    }

    public record ToolResultContentBlock : ContentBlock
    {
        [JsonPropertyName("tool_use_id")]
        public string? ToolUseId { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("is_error")]
        public bool? IsError { get; init; }

        [JsonPropertyName("content")]
        public JsonElement? Content { get; init; }
    }

    public record ServerToolUseContentBlock : ContentBlock
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    public record ServerToolResultContentBlock : ContentBlock
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    public record UsageInfo
    {
        [JsonPropertyName("input_tokens")]
        public int? InputTokens { get; init; }

        [JsonPropertyName("output_tokens")]
        public int? OutputTokens { get; init; }

        [JsonPropertyName("cache_read_input_tokens")]
        public int? CacheReadInputTokens { get; init; }

        [JsonPropertyName("cache_creation_input_tokens")]
        public int? CacheCreationInputTokens { get; init; }
    }
}

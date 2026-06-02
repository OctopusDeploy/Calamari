using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.Behaviours
{
    [JsonConverter(typeof(JsonStringEnumConverter<StreamEventType>))]
    public enum StreamEventType
    {
        [EnumMember(Value = "system")]
        System, //Infrastructure/metadata events,

        [EnumMember(Value = "assistant")]
        Assistant, // A message produced by the model.
        [EnumMember(Value = "user")]
        User, // A message in the user turn, which covers more than just human input:
        [EnumMember(Value = "result")]
        Result // The terminal event, summarizing the completed session.
    }

    [JsonConverter(typeof(JsonStringEnumConverter<ContentBlockType>))]
    public enum ContentBlockType
    {
        [EnumMember(Value = "text")]
        Text, // plain text response
        [EnumMember(Value = "thinking")]
        Thinking, // internal chain-of-thought (with a signature field for verification)
        [EnumMember(Value = "redacted_thinking")]
        RedactedThinking,
        [EnumMember(Value = "tool_use")]
        ToolUse, // the model invoking a tool (e.g. calling the Skill tool)
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

        [JsonPropertyName("uuid")]
        public string? Uuid { get; init; }
    }

    public record SystemStreamEvent : StreamEvent
    {
        [JsonPropertyName("subtype")]
        public string? Subtype { get; init; }

        // api_retry fields
        [JsonPropertyName("attempt")]
        public int? Attempt { get; init; }

        [JsonPropertyName("retry_delay_ms")]
        public int? RetryDelayMs { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("error_status")]
        public int? ErrorStatus { get; init; }

        // hook_started / hook_response fields
        [JsonPropertyName("hook_id")]
        public string? HookId { get; init; }

        [JsonPropertyName("hook_name")]
        public string? HookName { get; init; }

        [JsonPropertyName("hook_event")]
        public string? HookEvent { get; init; }

        [JsonPropertyName("output")]
        public string? Output { get; init; }

        [JsonPropertyName("stdout")]
        public string? Stdout { get; init; }

        [JsonPropertyName("stderr")]
        public string? Stderr { get; init; }

        [JsonPropertyName("exit_code")]
        public int? ExitCode { get; init; }

        [JsonPropertyName("outcome")]
        public string? Outcome { get; init; }

        // init fields
        [JsonPropertyName("cwd")]
        public string? Cwd { get; init; }

        [JsonPropertyName("tools")]
        public IReadOnlyList<string>? Tools { get; init; }

        [JsonPropertyName("mcp_servers")]
        public IReadOnlyList<McpServerStatus>? McpServers { get; init; }

        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("permissionMode")]
        public string? PermissionMode { get; init; }

        [JsonPropertyName("slash_commands")]
        public IReadOnlyList<string>? SlashCommands { get; init; }

        [JsonPropertyName("apiKeySource")]
        public string? ApiKeySource { get; init; }

        [JsonPropertyName("claude_code_version")]
        public string? ClaudeCodeVersion { get; init; }

        [JsonPropertyName("output_style")]
        public string? OutputStyle { get; init; }

        [JsonPropertyName("agents")]
        public IReadOnlyList<string>? Agents { get; init; }

        [JsonPropertyName("skills")]
        public IReadOnlyList<string>? Skills { get; init; }

        [JsonPropertyName("plugins")]
        public IReadOnlyList<PluginInfo>? Plugins { get; init; }

        [JsonPropertyName("fast_mode_state")]
        public string? FastModeState { get; init; }
    }

    public record McpServerStatus
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }
    }

    public record PluginInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("path")]
        public string? Path { get; init; }

        [JsonPropertyName("source")]
        public string? Source { get; init; }
    }

    public record AssistantStreamEvent : StreamEvent
    {
        [JsonPropertyName("message")]
        public StreamMessage? Message { get; init; }

        [JsonPropertyName("parent_tool_use_id")]
        public string? ParentToolUseId { get; init; }
    }

    public record UserStreamEvent : StreamEvent
    {
        [JsonPropertyName("message")]
        public StreamMessage? Message { get; init; }

        [JsonPropertyName("parent_tool_use_id")]
        public string? ParentToolUseId { get; init; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; init; }

        [JsonPropertyName("tool_use_result")]
        public ToolUseResultInfo? ToolUseResult { get; init; }

        [JsonPropertyName("isSynthetic")]
        public bool? IsSynthetic { get; init; }
    }

    public record ToolUseResultInfo
    {
        [JsonPropertyName("success")]
        public bool? Success { get; init; }

        [JsonPropertyName("commandName")]
        public string? CommandName { get; init; }
    }

    public record ResultStreamEvent : StreamEvent
    {
        [JsonPropertyName("subtype")]
        public string? Subtype { get; init; }

        [JsonPropertyName("is_error")]
        public bool? IsError { get; init; }

        [JsonPropertyName("result")]
        public string? Result { get; init; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; init; }

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
        public ResultUsageInfo? Usage { get; init; }

        [JsonPropertyName("modelUsage")]
        public IReadOnlyDictionary<string, ModelUsageInfo>? ModelUsage { get; init; }

        [JsonPropertyName("permission_denials")]
        public IReadOnlyList<string>? PermissionDenials { get; init; }

        [JsonPropertyName("fast_mode_state")]
        public string? FastModeState { get; init; }
    }

    public record StreamMessage
    {
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("role")]
        public string? Role { get; init; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; init; }

        [JsonPropertyName("stop_sequence")]
        public string? StopSequence { get; init; }

        [JsonPropertyName("usage")]
        public MessageUsageInfo? Usage { get; init; }

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

        [JsonPropertyName("signature")]
        public string? Signature { get; init; }
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

        [JsonPropertyName("caller")]
        public ToolUseCaller? Caller { get; init; }
    }

    public record ToolUseCaller
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }
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

    public record MessageUsageInfo
    {
        [JsonPropertyName("input_tokens")]
        public int? InputTokens { get; init; }

        [JsonPropertyName("output_tokens")]
        public int? OutputTokens { get; init; }

        [JsonPropertyName("cache_read_input_tokens")]
        public int? CacheReadInputTokens { get; init; }

        [JsonPropertyName("cache_creation_input_tokens")]
        public int? CacheCreationInputTokens { get; init; }

        [JsonPropertyName("cache_creation")]
        public CacheCreationInfo? CacheCreation { get; init; }

        [JsonPropertyName("service_tier")]
        public string? ServiceTier { get; init; }

        [JsonPropertyName("inference_geo")]
        public string? InferenceGeo { get; init; }
    }

    public record ResultUsageInfo
    {
        [JsonPropertyName("input_tokens")]
        public int? InputTokens { get; init; }

        [JsonPropertyName("output_tokens")]
        public int? OutputTokens { get; init; }

        [JsonPropertyName("cache_read_input_tokens")]
        public int? CacheReadInputTokens { get; init; }

        [JsonPropertyName("cache_creation_input_tokens")]
        public int? CacheCreationInputTokens { get; init; }

        [JsonPropertyName("server_tool_use")]
        public ServerToolUseUsage? ServerToolUse { get; init; }

        [JsonPropertyName("service_tier")]
        public string? ServiceTier { get; init; }

        [JsonPropertyName("cache_creation")]
        public CacheCreationInfo? CacheCreation { get; init; }

        [JsonPropertyName("inference_geo")]
        public string? InferenceGeo { get; init; }

        [JsonPropertyName("speed")]
        public string? Speed { get; init; }
    }

    public record ServerToolUseUsage
    {
        [JsonPropertyName("web_search_requests")]
        public int? WebSearchRequests { get; init; }

        [JsonPropertyName("web_fetch_requests")]
        public int? WebFetchRequests { get; init; }
    }

    public record CacheCreationInfo
    {
        [JsonPropertyName("ephemeral_5m_input_tokens")]
        public int? Ephemeral5mInputTokens { get; init; }

        [JsonPropertyName("ephemeral_1h_input_tokens")]
        public int? Ephemeral1hInputTokens { get; init; }
    }

    public record ModelUsageInfo
    {
        [JsonPropertyName("inputTokens")]
        public int? InputTokens { get; init; }

        [JsonPropertyName("outputTokens")]
        public int? OutputTokens { get; init; }

        [JsonPropertyName("cacheReadInputTokens")]
        public int? CacheReadInputTokens { get; init; }

        [JsonPropertyName("cacheCreationInputTokens")]
        public int? CacheCreationInputTokens { get; init; }

        [JsonPropertyName("webSearchRequests")]
        public int? WebSearchRequests { get; init; }

        [JsonPropertyName("costUSD")]
        public double? CostUsd { get; init; }

        [JsonPropertyName("contextWindow")]
        public int? ContextWindow { get; init; }

        [JsonPropertyName("maxOutputTokens")]
        public int? MaxOutputTokens { get; init; }
    }
}
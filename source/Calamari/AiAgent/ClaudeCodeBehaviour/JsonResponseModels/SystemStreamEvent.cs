using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;

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

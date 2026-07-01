using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.InjectionCheck;

public record InjectionCheckResult
{
    //todo: can we return cost here or only tokens?
    public required InjectionVerdict Verdict { get; init; }
    public required string Model { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
}

public record InjectionVerdict
{
    [JsonPropertyName("injectionDetected")]
    public bool InjectionDetected { get; init; }

    [JsonPropertyName("findings")]
    public List<InjectionFinding>? Findings { get; init; }
}

public record InjectionFinding
{
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("severity")]
    public string? Severity { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public class InjectionCheckException : Exception
{
    public InjectionCheckException(string message) : base(message)
    {
    }
}

record MessagesResponse
{
    [JsonPropertyName("content")]
    public List<MessageContentBlock>? Content { get; init; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("usage")]
    public MessageUsage? Usage { get; init; }
}

record MessageContentBlock
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

record MessageUsage
{
    [JsonPropertyName("input_tokens")]
    public int? InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int? OutputTokens { get; init; }
}

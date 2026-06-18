using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Octopus.Calamari.Contracts.ClaudeCode;

namespace Calamari.AiAgent.ClaudeCodeBehaviour
{
    public class ClaudeCodeStreamProcessor
    {
        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        readonly ILog log;
        readonly StringBuilder responseBuilder;

        public ClaudeCodeStreamProcessor(ILog log, StringBuilder responseBuilder)
        {
            this.log = log;
            this.responseBuilder = responseBuilder;
        }

        public ResultStreamEvent? Result { get; private set; }

        public void ProcessLine(string json)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                log.Verbose(json);
                return;
            }
            catch (Exception ex)
            {
                log.Error($"[stream] failed to parse JSON: {ex.Message}");
                return;
            }

            try
            {
                using var _ = doc;
                var typeString = doc.RootElement.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

                if (typeString == null || !TryParseEventType(typeString, out var eventType))
                {
                    log.Verbose($"[stream] unhandled event type '{typeString}'");
                    return;
                }

                switch (eventType)
                {
                    case StreamEventType.System:
                        HandleSystemEvent(JsonSerializer.Deserialize<SystemStreamEvent>(json, JsonOptions)!);
                        break;
                    case StreamEventType.Assistant:
                        HandleMessageEvent(JsonSerializer.Deserialize<AssistantStreamEvent>(json, JsonOptions)?.Message);
                        break;
                    case StreamEventType.User:
                        HandleUserMessage(JsonSerializer.Deserialize<UserStreamEvent>(json, JsonOptions));
                        break;
                    case StreamEventType.Result:
                        HandleResultEvent(JsonSerializer.Deserialize<ResultStreamEvent>(json, JsonOptions)!);
                        break;
                }
            }
            catch (Exception ex)
            {
                log.Error($"[stream] failed to process JSON: {ex.Message}");
            }
        }

        static bool TryParseEventType(string value, out StreamEventType result)
        {
            return value switch
            {
                "system" => Assign(StreamEventType.System, out result),
                "assistant" => Assign(StreamEventType.Assistant, out result),
                "user" => Assign(StreamEventType.User, out result),
                "result" => Assign(StreamEventType.Result, out result),
                _ => Assign(default, out result, false),
            };

            static bool Assign(StreamEventType val, out StreamEventType result, bool success = true)
            {
                result = val;
                return success;
            }
        }

        void HandleSystemEvent(SystemStreamEvent evt)
        {
            switch (evt.Subtype)
            {
                case "init":
                    break;

                case "api_retry":
                    log.Warn($"API retry (attempt {evt.Attempt}, {evt.RetryDelayMs}ms delay): {evt.Error}");
                    break;
            }
        }

        void HandleMessageEvent(StreamMessage? message, bool logText = true)
        {
            if (message?.Content == null)
                return;

            foreach (var element in message.Content)
            {
                var blockTypeStr = element.TryGetProperty("type", out var bt) ? bt.GetString() : null;

                if (blockTypeStr == null || !TryParseContentBlockType(blockTypeStr, out var blockType))
                {
                    log.Verbose($"[message] unhandled block type: {blockTypeStr}");
                    continue;
                }

                switch (blockType)
                {
                    case ContentBlockType.Text:
                    {
                        var block = element.Deserialize<TextContentBlock>(JsonOptions);
                        if (string.IsNullOrEmpty(block?.Text))
                        {
                            continue;
                        }
                        if (logText)
                        {
                            responseBuilder.Append(block?.Text);
                            log.Info(block?.Text ?? "");    
                        }
                        else
                        {
                            log.Verbose(block?.Text ?? "");
                        }
                        break;
                    }

                    case ContentBlockType.Thinking:
                    {
                        var block = element.Deserialize<ThinkingContentBlock>(JsonOptions);
                        log.Verbose($"[thinking] {block?.Thinking}");
                        break;
                    }

                    case ContentBlockType.RedactedThinking:
                        log.Verbose("[thinking] <redacted>");
                        break;

                    case ContentBlockType.ToolUse:
                    {
                        var block = element.Deserialize<ToolUseContentBlock>(JsonOptions);
                        log.Verbose(block?.Input.HasValue == true ?
                                        $"[tool] {block.Name} input: {block.Input}" :
                                        $"[tool] {block?.Name}");
                        break;
                    }

                    case ContentBlockType.ServerToolUse:
                    {
                        var block = element.Deserialize<ServerToolUseContentBlock>(JsonOptions);
                        log.Verbose($"[server_tool] {block?.Name}");
                        break;
                    }

                    case ContentBlockType.ServerToolResult:
                    {
                        var block = element.Deserialize<ServerToolResultContentBlock>(JsonOptions);
                        log.Verbose($"[server_tool] {block?.Name} completed");
                        break;
                    }

                    case ContentBlockType.ToolResult:
                    {
                        var block = element.Deserialize<ToolResultContentBlock>(JsonOptions);
                        log.Verbose(block?.IsError == true ?
                                        $"[tool_result] {block.ToolUseId} failed: {block.Content}" :
                                        $"[tool_result] {block?.Name} completed");
                        break;
                    }
                }
            }
        }

        static bool TryParseContentBlockType(string value, out ContentBlockType result)
        {
            return value switch
            {
                "text" => Assign(ContentBlockType.Text, out result),
                "thinking" => Assign(ContentBlockType.Thinking, out result),
                "redacted_thinking" => Assign(ContentBlockType.RedactedThinking, out result),
                "tool_use" => Assign(ContentBlockType.ToolUse, out result),
                "tool_result" => Assign(ContentBlockType.ToolResult, out result),
                "server_tool_use" => Assign(ContentBlockType.ServerToolUse, out result),
                "server_tool_result" => Assign(ContentBlockType.ServerToolResult, out result),
                _ => Assign(default, out result, false),
            };

            static bool Assign(ContentBlockType val, out ContentBlockType result, bool success = true)
            {
                result = val;
                return success;
            }
        }

        void HandleUserMessage(UserStreamEvent? message)
        {
            if (message is null || message?.Message == null)
                return;

            if (message.IsSynthetic == true)
            {
                return; //TODO: Still log
            }
            HandleMessageEvent(message.Message, logText: false);
        }

        void HandleResultEvent(ResultStreamEvent evt)
        {
            Result = evt;

            if (evt.Result != null && responseBuilder.Length == 0)
            {
                responseBuilder.Append(evt.Result);
                log.Info(evt.Result);
            }

            var properties = new Dictionary<string, string>();

            if (evt.CostUsd.HasValue)
                properties[ClaudeCodeServiceMessages.Usage.CostUsdAttribute] = evt.CostUsd.Value.ToString("F6");
            if (evt.TotalCostUsd.HasValue)
                properties[ClaudeCodeServiceMessages.Usage.TotalCostUsdAttribute] = evt.TotalCostUsd.Value.ToString("F6");
            if (evt.DurationMs.HasValue)
                properties[ClaudeCodeServiceMessages.Usage.DurationMsAttribute] = evt.DurationMs.Value.ToString("F0");
            if (evt.DurationApiMs.HasValue)
                properties[ClaudeCodeServiceMessages.Usage.DurationApiMsAttribute] = evt.DurationApiMs.Value.ToString("F0");
            if (evt.NumTurns.HasValue)
                properties[ClaudeCodeServiceMessages.Usage.NumTurnsAttribute] = evt.NumTurns.Value.ToString();
            log.Info($"AI Agent Usage — Cost: ${evt.CostUsd} USD (total: ${evt.TotalCostUsd}), Duration: {evt.DurationMs}ms, Turns: {evt.NumTurns}");
            
            if (evt.Usage is { } usage)
            {
                if (usage.InputTokens.HasValue)
                    properties[ClaudeCodeServiceMessages.Usage.InputTokensAttribute] = usage.InputTokens.Value.ToString();
                if (usage.OutputTokens.HasValue)
                    properties[ClaudeCodeServiceMessages.Usage.OutputTokensAttribute] = usage.OutputTokens.Value.ToString();
                if (usage.CacheReadInputTokens.HasValue)
                    properties[ClaudeCodeServiceMessages.Usage.CacheReadInputTokensAttribute] = usage.CacheReadInputTokens.Value.ToString();
                if (usage.CacheCreationInputTokens.HasValue)
                    properties[ClaudeCodeServiceMessages.Usage.CacheCreationInputTokensAttribute] = usage.CacheCreationInputTokens.Value.ToString();
                
                log.Info($"AI Agent Tokens — Input: {usage.InputTokens}, Output: {usage.OutputTokens}, Cache read: {usage.CacheReadInputTokens}, Cache creation: {usage.CacheCreationInputTokens}");
            }

            log.WriteServiceMessage(new ServiceMessage(ClaudeCodeServiceMessages.Usage.Name, properties));
        }
    }
}

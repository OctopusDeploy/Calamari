using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;

namespace Calamari.AiAgent.Behaviours
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

        public void ProcessLine(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var type = doc.RootElement.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

            switch (type)
            {
                case "system":
                    HandleSystemEvent(JsonSerializer.Deserialize<SystemStreamEvent>(json, JsonOptions)!);
                    break;
                case "assistant":
                    HandleMessageEvent(JsonSerializer.Deserialize<AssistantStreamEvent>(json, JsonOptions)?.Message);
                    break;
                case "user":
                    HandleUserMessage(JsonSerializer.Deserialize<UserStreamEvent>(json, JsonOptions)?.Message);
                    break;
                case "result":
                    HandleResultEvent(JsonSerializer.Deserialize<ResultStreamEvent>(json, JsonOptions)!);
                    break;
                default:
                    log.Verbose($"[stream] unhandled event type '{type}'");
                    break;
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

        void HandleMessageEvent(StreamMessage? message)
        {
            if (message?.Content == null)
                return;

            foreach (var element in message.Content)
            {
                var blockType = element.TryGetProperty("type", out var bt) ? bt.GetString() : null;

                switch (blockType)
                {
                    case "text":
                    {
                        var block = element.Deserialize<TextContentBlock>(JsonOptions);
                        responseBuilder.Append(block?.Text);
                        log.Info(block?.Text ?? "");
                        break;
                    }

                    case "thinking":
                    {
                        var block = element.Deserialize<ThinkingContentBlock>(JsonOptions);
                        log.Verbose($"[thinking] {block?.Thinking}");
                        break;
                    }

                    case "redacted_thinking":
                        log.Verbose("[thinking] <redacted>");
                        break;

                    case "tool_use":
                    {
                        var block = element.Deserialize<ToolUseContentBlock>(JsonOptions);
                        log.Info($"[tool] {block?.Name}");
                        if (block?.Input.HasValue == true)
                            log.Verbose($"[tool] {block.Name} input: {block.Input}");
                        break;
                    }

                    case "server_tool_use":
                    {
                        var block = element.Deserialize<ServerToolUseContentBlock>(JsonOptions);
                     //   log.Info($"[server_tool] {block?.Name}");
                        break;
                    }

                    case "server_tool_result":
                    {
                        var block = element.Deserialize<ServerToolResultContentBlock>(JsonOptions);
                       // log.Verbose($"[server_tool] {block?.Name} completed");
                        break;
                    }

                    case "tool_result":
                    {
                        var block = element.Deserialize<ToolResultContentBlock>(JsonOptions);
                        /*
                        if (block?.IsError == true)
                            log.Warn($"[tool_result] {block.ToolUseId} failed: {block.Content}");
                        else
                            log.Verbose($"[tool_result] {block?.Name} completed");
                        */
                        break;
                     
                    }

                    default:
                        log.Verbose($"[message] unhandled block type: {blockType}");
                        break;
                }
            }
        }

        void HandleUserMessage(StreamMessage? message)
        {
            HandleMessageEvent(message);
        }

        void HandleResultEvent(ResultStreamEvent evt)
        {
            if (evt.Result != null && responseBuilder.Length == 0)
            {
                responseBuilder.Append(evt.Result);
                log.Info(evt.Result);
            }

            if (evt.CostUsd.HasValue)
                log.Info($"Cost: ${evt.CostUsd.Value:F4} USD");

            if (evt.TotalCostUsd.HasValue)
                log.Info($"Total cost: ${evt.TotalCostUsd.Value:F4} USD");

            if (evt.DurationMs.HasValue)
                log.Info($"Duration: {evt.DurationMs.Value / 1000.0:F1}s");

            if (evt.DurationApiMs.HasValue)
                log.Verbose($"API duration: {evt.DurationApiMs.Value / 1000.0:F1}s");

            if (evt.NumTurns.HasValue)
                log.Info($"Turns: {evt.NumTurns.Value}");

            if (evt.Usage is { } usage)
            {
                log.Info($"Tokens — input: {usage.InputTokens ?? 0}, output: {usage.OutputTokens ?? 0}, cache read: {usage.CacheReadInputTokens ?? 0}, cache creation: {usage.CacheCreationInputTokens ?? 0}");
            }

            EmitUsageServiceMessage(evt);
        }

        void EmitUsageServiceMessage(ResultStreamEvent evt)
        {
            var properties = new Dictionary<string, string>();

            if (evt.CostUsd.HasValue)
                properties[AiAgentServiceMessageNames.CostUsdAttribute] = evt.CostUsd.Value.ToString("F6");
            if (evt.TotalCostUsd.HasValue)
                properties[AiAgentServiceMessageNames.TotalCostUsdAttribute] = evt.TotalCostUsd.Value.ToString("F6");
            if (evt.DurationMs.HasValue)
                properties[AiAgentServiceMessageNames.DurationMsAttribute] = evt.DurationMs.Value.ToString("F0");
            if (evt.DurationApiMs.HasValue)
                properties[AiAgentServiceMessageNames.DurationApiMsAttribute] = evt.DurationApiMs.Value.ToString("F0");
            if (evt.NumTurns.HasValue)
                properties[AiAgentServiceMessageNames.NumTurnsAttribute] = evt.NumTurns.Value.ToString();

            if (evt.Usage is { } usage)
            {
                if (usage.InputTokens.HasValue)
                    properties[AiAgentServiceMessageNames.InputTokensAttribute] = usage.InputTokens.Value.ToString();
                if (usage.OutputTokens.HasValue)
                    properties[AiAgentServiceMessageNames.OutputTokensAttribute] = usage.OutputTokens.Value.ToString();
                if (usage.CacheReadInputTokens.HasValue)
                    properties[AiAgentServiceMessageNames.CacheReadInputTokensAttribute] = usage.CacheReadInputTokens.Value.ToString();
                if (usage.CacheCreationInputTokens.HasValue)
                    properties[AiAgentServiceMessageNames.CacheCreationInputTokensAttribute] = usage.CacheCreationInputTokens.Value.ToString();
            }

            log.WriteServiceMessage(new ServiceMessage(AiAgentServiceMessageNames.Name, properties));
        }
    }
}

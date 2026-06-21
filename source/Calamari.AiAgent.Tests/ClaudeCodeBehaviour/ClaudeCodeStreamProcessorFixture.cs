using System.Linq;
using System.Text;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Calamari.Contracts.ClaudeCode;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class ClaudeCodeStreamProcessorFixture
{
    InMemoryLog log = null!;
    StringBuilder responseBuilder = null!;
    ClaudeCodeStreamProcessor processor = null!;

    [SetUp]
    public void SetUp()
    {
        log = new InMemoryLog();
        responseBuilder = new StringBuilder();
        processor = new ClaudeCodeStreamProcessor(log, responseBuilder);
    }

    [Test]
    public void TextContentBlock_AppendsToResponseAndLogsInfo()
    {
        var json = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"Hello world"}]}}
            """;

        processor.ProcessLine(json);

        responseBuilder.ToString().Should().Be("Hello world");
        log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Info && m.FormattedMessage.Contains("Hello world"));
    }

    [Test]
    public void ThinkingBlock_LogsVerbose()
    {
        var json = """
            {"type":"assistant","message":{"content":[{"type":"thinking","thinking":"Let me reason about this"}]}}
            """;

        processor.ProcessLine(json);

        responseBuilder.ToString().Should().BeEmpty();
        log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage.Contains("Let me reason about this"));
    }

    [Test]
    public void RedactedThinkingBlock_LogsRedactedMessage()
    {
        var json = """
            {"type":"assistant","message":{"content":[{"type":"redacted_thinking"}]}}
            """;

        processor.ProcessLine(json);

        responseBuilder.ToString().Should().BeEmpty();
        log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage.Contains("<redacted>"));
    }

    [Test]
    public void ToolUseBlock_LogsToolName()
    {
        var json = """
            {"type":"assistant","message":{"content":[{"type":"tool_use","name":"Read","id":"toolu_123","input":{"file_path":"/tmp/test.txt"}}]}}
            """;

        processor.ProcessLine(json);

        responseBuilder.ToString().Should().BeEmpty();
        log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage.Contains("Read"));
    }

    [Test]
    public void ToolResultError_LogsWithFailedMessage()
    {
        var json = """
            {"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"toolu_123","is_error":true,"content":"File not found"}]}}
            """;

        processor.ProcessLine(json);

        log.Messages.Should().Contain(m => m.FormattedMessage.Contains("toolu_123") && m.FormattedMessage.Contains("failed"));
    }

    [Test]
    public void ToolResultSuccess_LogsVerbose()
    {
        var json = """
            {"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"toolu_123","name":"Read","is_error":false}]}}
            """;

        processor.ProcessLine(json);

        log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage.Contains("Read") && m.FormattedMessage.Contains("completed"));
    }

    [Test]
    public void ResultEvent_EmitsUsageServiceMessage()
    {
        var json = """
            {"type":"result","result":"Paris","cost_usd":0.003,"total_cost_usd":0.003,"duration_ms":4521,"duration_api_ms":3200,"num_turns":1,"usage":{"input_tokens":100,"output_tokens":50,"cache_read_input_tokens":10,"cache_creation_input_tokens":5}}
            """;

        processor.ProcessLine(json);

        log.ServiceMessages.Should().Contain(m => m.Name == ClaudeCodeServiceMessages.Usage.Name);

        var msg = log.ServiceMessages.First(m => m.Name == ClaudeCodeServiceMessages.Usage.Name);
        msg.GetValue(ClaudeCodeServiceMessages.Usage.CostUsdAttribute).Should().NotBeNull();
        msg.GetValue(ClaudeCodeServiceMessages.Usage.TotalCostUsdAttribute).Should().NotBeNull();
        msg.GetValue(ClaudeCodeServiceMessages.Usage.DurationMsAttribute).Should().NotBeNull();
        msg.GetValue(ClaudeCodeServiceMessages.Usage.NumTurnsAttribute).Should().NotBeNull();
        msg.GetValue(ClaudeCodeServiceMessages.Usage.InputTokensAttribute).Should().NotBeNull();
        msg.GetValue(ClaudeCodeServiceMessages.Usage.OutputTokensAttribute).Should().NotBeNull();
        msg.GetValue(ClaudeCodeServiceMessages.Usage.CacheReadInputTokensAttribute).Should().NotBeNull();
        msg.GetValue(ClaudeCodeServiceMessages.Usage.CacheCreationInputTokensAttribute).Should().NotBeNull();
    }

    [Test]
    public void ResultEvent_EmitsModelUsageAsJson_WhenModelUsagePresent()
    {
        var json = """
            {"type":"result","result":"Paris","cost_usd":0.003,"total_cost_usd":0.005,"duration_ms":4521,"duration_api_ms":3200,"num_turns":1,"modelUsage":{"claude-opus-4-5":{"inputTokens":80,"outputTokens":40,"cacheReadInputTokens":5,"cacheCreationInputTokens":3,"costUSD":0.002},"claude-haiku-4-5":{"inputTokens":20,"outputTokens":10,"costUSD":0.001}}}
            """;

        processor.ProcessLine(json);

        log.ServiceMessages.Count(m => m.Name == ClaudeCodeServiceMessages.Usage.Name).Should().Be(1);

        var msg = log.ServiceMessages.First(m => m.Name == ClaudeCodeServiceMessages.Usage.Name);
        msg.GetValue(ClaudeCodeServiceMessages.Usage.CostUsdAttribute).Should().NotBeNull();
        msg.GetValue(ClaudeCodeServiceMessages.Usage.TotalCostUsdAttribute).Should().NotBeNull();
        msg.GetValue(ClaudeCodeServiceMessages.Usage.DurationMsAttribute).Should().NotBeNull();
        msg.GetValue(ClaudeCodeServiceMessages.Usage.NumTurnsAttribute).Should().Be("1");

        var modelUsageJson = msg.GetValue(ClaudeCodeServiceMessages.Usage.ModelUsageAttribute);
        modelUsageJson.Should().NotBeNull();
        modelUsageJson.Should().Contain("claude-opus-4-5");
        modelUsageJson.Should().Contain("claude-haiku-4-5");
    }

    [Test]
    public void ResultEvent_FallsBackToResultText_WhenNoAssistantText()
    {
        var json = """
            {"type":"result","result":"Paris","cost_usd":0.001}
            """;

        processor.ProcessLine(json);

        responseBuilder.ToString().Should().Be("Paris");
    }

    [Test]
    public void ResultEvent_DoesNotOverwriteAssistantText()
    {
        var assistantJson = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"The capital is Paris"}]}}
            """;
        var resultJson = """
            {"type":"result","result":"The capital is Paris","cost_usd":0.001}
            """;

        processor.ProcessLine(assistantJson);
        processor.ProcessLine(resultJson);

        responseBuilder.ToString().Should().Be("The capital is Paris");
    }

    [Test]
    public void UnknownEventType_LogsVerboseAndDoesNotThrow()
    {
        var json = """{"type":"stream_event","data":"something"}""";

        var act = () => processor.ProcessLine(json);

        act.Should().NotThrow();
        log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage.Contains("unhandled event type"));
    }

    [Test]
    public void UnknownContentBlockType_LogsVerboseAndContinues()
    {
        var json = """
            {"type":"assistant","message":{"content":[{"type":"citations","data":"ref1"},{"type":"text","text":"Answer"}]}}
            """;

        var act = () => processor.ProcessLine(json);

        act.Should().NotThrow();
        responseBuilder.ToString().Should().Be("Answer");
        log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage.Contains("unhandled block type"));
    }

    [Test]
    public void UnknownSystemSubtype_DoesNotThrow()
    {
        var json = """
            {"type":"system","subtype":"some_new_subtype","data":"whatever"}
            """;

        var act = () => processor.ProcessLine(json);

        act.Should().NotThrow();
    }

    [Test]
    public void ApiRetry_LogsWarning()
    {
        var json = """
            {"type":"system","subtype":"api_retry","attempt":2,"retry_delay_ms":5000,"error":"rate_limit","error_status":429}
            """;

        processor.ProcessLine(json);

        log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Warn && m.FormattedMessage.Contains("rate_limit") && m.FormattedMessage.Contains("5000"));
    }

    [Test]
    public void MalformedJson_DoesNotThrow()
    {
        var act = () => processor.ProcessLine("this is not json {{{");

        act.Should().NotThrow();
    }

    [Test]
    public void MultipleTextBlocks_ConcatenatesResponse()
    {
        var json1 = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"Hello "}]}}
            """;
        var json2 = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"world"}]}}
            """;

        processor.ProcessLine(json1);
        processor.ProcessLine(json2);

        responseBuilder.ToString().Should().Be("Hello world");
    }

    [Test]
    public void ServerToolUse_LogsVerbose()
    {
        var json = """
            {"type":"assistant","message":{"content":[{"type":"server_tool_use","name":"web_search"}]}}
            """;

        processor.ProcessLine(json);

        log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage.Contains("web_search"));
    }

    [Test]
    public void NullMessageContent_DoesNotThrow()
    {
        var json = """{"type":"assistant","message":{"content":null}}""";

        var act = () => processor.ProcessLine(json);

        act.Should().NotThrow();
    }

    [Test]
    public void UserTextContent_DoesNotAppendToResponse()
    {
        var json = """
            {"type":"user","message":{"content":[{"type":"text","text":"user input"}]}}
            """;

        processor.ProcessLine(json);

        responseBuilder.ToString().Should().BeEmpty();
        log.Messages.Should().Contain(m => m.Level == InMemoryLog.Level.Verbose && m.FormattedMessage.Contains("user input"));
    }

    [Test]
    public void SyntheticUserMessage_IsSkipped()
    {
        var json = """
            {"type":"user","message":{"content":[{"type":"text","text":"synthetic"}]},"isSynthetic":true}
            """;

        processor.ProcessLine(json);

        log.Messages.Should().NotContain(m => m.FormattedMessage.Contains("synthetic"));
    }
}
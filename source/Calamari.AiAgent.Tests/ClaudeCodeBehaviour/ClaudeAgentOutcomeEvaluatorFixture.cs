using System;
using System.Collections.Generic;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;
using Calamari.Common.Commands;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class ClaudeAgentOutcomeEvaluatorFixture
{
    [Test]
    public void ExitZeroWithSuccessResult_DoesNotThrow()
    {
        var result = new ResultStreamEvent { Subtype = "success", IsError = false };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().NotThrow();
    }

    [Test]
    public void NullResult_WithZeroExit_DoesNotThrow()
    {
        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, null);

        act.Should().NotThrow();
    }

    [Test]
    public void NonZeroExit_Throws_WithExitCodeInMessage()
    {
        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(2, new ResultStreamEvent { Subtype = "success", IsError = false });

        act.Should().Throw<CommandException>().WithMessage("*exited with code 2*");
    }

    [Test]
    public void NonSuccessSubtype_Throws_WithSubtypeAndStopReason()
    {
        var result = new ResultStreamEvent { Subtype = "error_max_turns", IsError = false, StopReason = "max_turns" };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().Throw<CommandException>()
           .Which.Message.Should().Contain("error_max_turns").And.Contain("max_turns");
    }

    [Test]
    public void IsErrorTrue_WithSuccessSubtype_Throws()
    {
        var result = new ResultStreamEvent { Subtype = "success", IsError = true };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().Throw<CommandException>().WithMessage("*did not complete successfully*");
    }

    [Test]
    public void ExitZero_SuccessSubtype_WithPermissionDenials_Throws_NamingDeniedTools()
    {
        var result = new ResultStreamEvent
        {
            Subtype = "success",
            IsError = false,
            PermissionDenials = new List<PermissionDenial>
            {
                new() { ToolName = "Bash", ToolUseId = "toolu_1" },
                new() { ToolName = "WebFetch", ToolUseId = "toolu_2" },
            },
        };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().Throw<CommandException>()
           .Which.Message.Should().Contain("denied permission").And.Contain("Bash").And.Contain("WebFetch");
    }

    [Test]
    public void FailureSignal_WithReason_Throws_IncludingReason()
    {
        var result = new ResultStreamEvent
        {
            Subtype = "success",
            IsError = false,
            Result = "<octopus-task-failed>Smoke test returned HTTP 500 from /health after 3 retries.</octopus-task-failed>",
        };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().Throw<CommandException>()
           .Which.Message.Should().Contain("step should fail").And.Contain("Smoke test returned HTTP 500");
    }

    [Test]
    public void FailureSignal_WithMultiLineReason_Throws_PreservingReason()
    {
        var result = new ResultStreamEvent
        {
            Subtype = "success",
            IsError = false,
            Result = "<octopus-task-failed>\nHealth check failed:\n- /health returned 500\n- /ready timed out\n</octopus-task-failed>",
        };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().Throw<CommandException>()
           .Which.Message.Should().Contain("/health returned 500").And.Contain("/ready timed out");
    }

    [Test]
    public void FailureSignal_EmptyBlock_Throws_WithGenericMessage()
    {
        var result = new ResultStreamEvent { Subtype = "success", IsError = false, Result = "<octopus-task-failed></octopus-task-failed>" };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().Throw<CommandException>().WithMessage("*step should fail*");
    }

    [Test]
    public void FailureSignal_SelfClosing_Throws_WithGenericMessage()
    {
        var result = new ResultStreamEvent { Subtype = "success", IsError = false, Result = "<octopus-task-failed/>" };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().Throw<CommandException>().WithMessage("*step should fail*");
    }

    [Test]
    public void FailureSignal_WithinLargerResult_Throws()
    {
        var result = new ResultStreamEvent
        {
            Subtype = "success",
            IsError = false,
            Result = "I checked the deployment health.\n<octopus-task-failed>Health check is red.</octopus-task-failed>\nDone.",
        };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().Throw<CommandException>().Which.Message.Should().Contain("Health check is red");
    }

    [Test]
    public void FailureSignal_TakesPrecedenceOverNonSuccessSubtype()
    {
        var result = new ResultStreamEvent
        {
            Subtype = "error_max_turns",
            IsError = true,
            Result = "<octopus-task-failed>Validation failed.</octopus-task-failed>",
        };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().Throw<CommandException>().Which.Message.Should().Contain("Validation failed");
    }

    [Test]
    public void UnclosedFailureSignal_DoesNotThrow()
    {
        // A truncated message never wrote the closing tag, so we cannot treat it as a deliberate, complete failure.
        var result = new ResultStreamEvent
        {
            Subtype = "success",
            IsError = false,
            Result = "<octopus-task-failed>Health check is red and then the message was cut off",
        };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().NotThrow();
    }

    [Test]
    public void ResultWithoutFailureSignal_DoesNotThrow()
    {
        var result = new ResultStreamEvent
        {
            Subtype = "success",
            IsError = false,
            Result = "The deployment looks healthy. No failure conditions were met.",
        };

        Action act = () => ClaudeAgentOutcomeEvaluator.EnsureSuccessful(0, result);

        act.Should().NotThrow();
    }
}

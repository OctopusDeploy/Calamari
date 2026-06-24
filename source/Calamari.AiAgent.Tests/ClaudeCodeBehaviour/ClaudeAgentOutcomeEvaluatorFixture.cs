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
}

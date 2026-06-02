using Calamari.AiAgent.Behaviours;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests;

[TestFixture]
public class ClaudeCommandArgsBuilderFixture
{
    ClaudeCommandArgsBuilder MinimalBuilder() =>
        new ClaudeCommandArgsBuilder()
            .WithPrompt("test prompt")
            .WithModel("claude-sonnet-4-20250514");

    [Test]
    public void Build_IncludesRequiredFlags()
    {
        var args = MinimalBuilder().Build();

        args.Should().Contain("-p");
        args.Should().Contain("--model claude-sonnet-4-20250514");
        args.Should().Contain("--output-format stream-json");
        args.Should().Contain("--verbose");
        args.Should().Contain("--permission-mode dontAsk");
        args.Should().Contain("--no-session-persistence");
    }

    [Test]
    public void Build_DefaultsMaxTurnsTo10_WhenNotSet()
    {
        var args = MinimalBuilder().Build();

        args.Should().Contain("--max-turns 10");
    }

    [Test]
    public void Build_UsesProvidedMaxTurns_WhenSet()
    {
        var args = MinimalBuilder().WithMaxTurns(5).Build();

        args.Should().Contain("--max-turns 5");
        args.Should().NotContain("--max-turns 10");
    }

    [Test]
    public void Build_OmitsMaxBudgetUsd_WhenNotSet()
    {
        var args = MinimalBuilder().Build();

        args.Should().NotContain("--max-budget-usd");
    }

    [Test]
    public void Build_IncludesMaxBudgetUsd_WhenSet()
    {
        var args = MinimalBuilder().WithMaxBudgetUsd(1.50m).Build();

        args.Should().Contain("--max-budget-usd 1.50");
    }

    [Test]
    public void Build_IncludesAllowedTools_WhenSet()
    {
        var args = MinimalBuilder()
            .WithAllowedTools(new[] { "Read", "Bash" })
            .Build();

        args.Should().Contain("--allowedTools Read,Bash");
    }

    [Test]
    public void Build_OmitsAllowedTools_WhenEmpty()
    {
        var args = MinimalBuilder()
            .WithAllowedTools(new string[0])
            .Build();

        args.Should().NotContain("--allowedTools");
    }

    [Test]
    public void Build_IncludesSystemPrompt_WhenSet()
    {
        var args = MinimalBuilder()
            .WithSystemPrompt("You are helpful")
            .Build();

        args.Should().Contain("--system-prompt");
        args.Should().Contain("You are helpful");
    }

    [Test]
    public void Build_OmitsSystemPrompt_WhenNotSet()
    {
        var args = MinimalBuilder().Build();

        args.Should().NotContain("--system-prompt");
    }

    [Test]
    public void Build_EscapesPromptWithSpaces()
    {
        var args = new ClaudeCommandArgsBuilder()
            .WithPrompt("What is the capital of France?")
            .WithModel("claude-sonnet-4-20250514")
            .Build();

        args.Should().Contain("\"What is the capital of France?\"");
    }

    [Test]
    public void Build_ThrowsWhenPromptNotSet()
    {
        var builder = new ClaudeCommandArgsBuilder()
            .WithModel("claude-sonnet-4-20250514");

        var act = () => builder.Build();

        act.Should().Throw<System.InvalidOperationException>()
            .WithMessage("*prompt*");
    }
}

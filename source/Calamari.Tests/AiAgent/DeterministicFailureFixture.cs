using System;
using System.Threading.Tasks;
using Calamari.Testing;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests;

[TestFixture]
[Explicit("Exercises the real claude CLI end-to-end; most cases need ANTHROPIC_KEY.")]
[Category("Integration")]
public class DeterministicFailureFixture
{
    const string Model = "claude-sonnet-4-5-20250929";

    static string AnthropicKey => Environment.GetEnvironmentVariable("ANTHROPIC_KEY");

    static void RequireAnthropicKey()
    {
        if (string.IsNullOrWhiteSpace(AnthropicKey))
            Assert.Ignore("ANTHROPIC_KEY is not set.");
    }

    [Test]
    public async Task InvalidApiKey_FailsStep()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.ApiKey, "sk-ant-invalid-test-000");
                context.Variables.Add(SpecialVariables.Action.Claude.MaxTurns, "1");
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "Reply with exactly: DONE");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeFalse();
    }

    [Test]
    public async Task SimplePrompt_SucceedsStep()
    {
        RequireAnthropicKey();

        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.ApiKey, AnthropicKey);
                context.Variables.Add(SpecialVariables.Action.Claude.Model, Model);
                context.Variables.Add(SpecialVariables.Action.Claude.MaxTurns, "3");
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt,
                    "What is the capital of France? Reply with just the city name.");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.FullLog.Should().Contain("Paris");
    }
}

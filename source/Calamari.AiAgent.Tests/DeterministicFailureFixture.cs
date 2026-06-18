using System;
using System.Threading.Tasks;
using Calamari.Testing;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests;

[TestFixture]
[Explicit("Exercises the real claude CLI end-to-end; most cases need ANTHROPIC_TOKEN.")]
[Category("Integration")]
public class DeterministicFailureFixture
{
    const string Model = "claude-sonnet-4-5-20250929";

    static string Token => Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN");

    static void RequireToken()
    {
        if (string.IsNullOrWhiteSpace(Token))
            Assert.Ignore("ANTHROPIC_TOKEN is not set.");
    }

    // Needs no token: a bad key fails auth and exits non-zero.
    [Test]
    public async Task InvalidApiKey_FailsStep()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.ApiToken, "sk-ant-invalid-test-000");
                context.Variables.Add(SpecialVariables.Action.Claude.MaxTurns, "1");
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "Reply with exactly: DONE");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeFalse();
    }

    [Test]
    public async Task SimplePrompt_SucceedsStep()
    {
        RequireToken();

        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.ApiToken, Token);
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

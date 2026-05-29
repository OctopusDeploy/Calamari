using System;
using System.Threading.Tasks;
using Calamari.Testing;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests;

[TestFixture]
public class RunAgentCommandFixture
{
    [Test]
    [Category("PlatformAgnostic")]
    public async Task FailsWhenPromptIsMissing()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.AiAgent.ApiToken, "fake-api-token");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeFalse();
    }

    [Test]
    [Category("PlatformAgnostic")]
    public async Task FailsWhenApiTokenIsMissing()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.AiAgent.Prompt, "Hello");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeFalse();
    }

    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_SucceedsWithSimplePrompt()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.AiAgent.ApiToken, Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN"));
                context.Variables.Add(SpecialVariables.Action.AiAgent.Prompt, "What is the capital of France? Reply with just the city name.");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.FullLog.Should().Contain("Paris");
    }

    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_EmitsUsageServiceMessage()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.AiAgent.ApiToken, Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN"));
                context.Variables.Add(SpecialVariables.Action.AiAgent.Prompt, "Reply with just the word 'hello'.");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.ServiceMessages.Should().Contain(m => m.Name == AiAgentServiceMessageNames.Name);
    }

    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_SucceedsWithWebFetch()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.AiAgent.ApiToken, Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN"));
                context.Variables.Add(SpecialVariables.Action.AiAgent.RunAsUsername, "test-user");
                context.Variables.Add(SpecialVariables.Action.AiAgent.Prompt, "get the currently executing process user");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.FullLog.Should().Contain("origin");
    }
}

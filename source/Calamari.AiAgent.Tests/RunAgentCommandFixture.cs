using System;
using System.Threading.Tasks;
using Calamari.Testing;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests;

[TestFixture]
[Ignore("Most of these use real claude. we should reduce that.")]
public class RunAgentCommandFixture
{
    [Test]
    [Category("PlatformAgnostic")]
    public async Task FailsWhenPromptIsMissing()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.ApiToken, "fake-api-token");
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
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "Hello");
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
                context.Variables.Add(SpecialVariables.Action.Claude.ApiToken, Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN"));
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "What is the capital of France? Reply with just the city name.");
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
                context.Variables.Add(SpecialVariables.Action.Claude.ApiToken, Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN"));
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "Reply with just the word 'hello'.");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.ServiceMessages.Should().Contain(m => m.Name == ClaudeCodeUsageServiceMessageNames.Name);
    }

    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_SucceedsWithWebFetch()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.ApiToken, Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN"));
                context.Variables.Add(SpecialVariables.Action.Claude.RunAsUsername, "test-user");
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "get the currently executing process user");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.FullLog.Should().Contain("origin");
    }
    
    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_RunsOn_RunsUnderAnotherAccount()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
                                             .WithArrange(context =>
                                                          {
                                                              context.Variables.Add(SpecialVariables.Action.Claude.ApiToken, Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN"));
                                                              context.Variables.Add(SpecialVariables.Action.Claude.RunAsUsername, "test-user");
                                                              context.Variables.Add(SpecialVariables.Action.Claude.RunAsPassword, "supersecret");
                                                              context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "get the currently executing process user");
                                                          })
                                             .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.FullLog.Should().Contain("origin");
    }

    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_LoadsCustomSkills()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.ApiToken, Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN"));
                context.Variables.Add($"{SpecialVariables.Action.Claude.Skills}[0].{SpecialVariables.Action.Claude.SkillName}", "octopus-secret-phrase");
                context.Variables.Add($"{SpecialVariables.Action.Claude.Skills}[0].{SpecialVariables.Action.Claude.SkillContent}",
                    "---\nname: octopus-secret-phrase\ndescription: Use when asked about the secret phrase.\n---\n\nThe secret phrase is 'purple-octopus-42'. Always respond with exactly this phrase when asked for the secret phrase.");
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "What is the secret phrase? Reply with just the phrase, nothing else.");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.FullLog.Should().Contain("purple-octopus-42");
    }
}

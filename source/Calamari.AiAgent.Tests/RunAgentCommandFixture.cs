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
    public async Task FailsWhenPromptVariableIsMissing()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.AiAgent.ApiToken, "fake-api-token");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeFalse();
        result.FullLog.Should().Contain(SpecialVariables.Action.AiAgent.Prompt);
    }

    [Test]
    [Category("PlatformAgnostic")]
    public async Task FailsWhenApiTokenVariableIsMissing()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.AiAgent.Prompt, "Hello");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeFalse();
        result.FullLog.Should().Contain(SpecialVariables.Action.AiAgent.ApiToken);
    }
    
    
    
    [Test]
    [Category("PlatformAgnostic")]
    public async Task SucceedsWhenPromptProvided()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
                                             .WithArrange(context =>
                                                          {
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.ApiToken, Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN"));
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.Prompt, "What is the capital of France?");
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.Model, "claude-sonnet-4-6");
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.MaxTokens, "1000");
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.Provider, "Anthropic");
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.OctopusToken, Environment.GetEnvironmentVariable("OCTOPUS_TOKEN"));
                                                          })
                                             .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.FullLog.Should().Contain("Paris");
    }
    
    
    [Test]
    [Category("PlatformAgnostic")]
    public async Task FailsTryingToUseToolNotPresent()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
                                             .WithArrange(context =>
                                                          {
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.ApiToken, Environment.GetEnvironmentVariable("ANTHROPIC_TOKEN"));
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.Prompt, "Get the current news item on the front page of the New York Times.");
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.Model, "claude-sonnet-4-6");
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.MaxTokens, "1000");
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.Provider, "Anthropic");
                                                              context.Variables.Add(SpecialVariables.Action.AiAgent.OctopusToken, Environment.GetEnvironmentVariable("OCTOPUS_TOKEN"));
                                                          })
                                             .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.FullLog.Should().Contain("Paris");
    }
}

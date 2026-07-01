using System;
using System.Threading.Tasks;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Testing;
using Calamari.Testing.LogParser;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Calamari.Contracts.ClaudeCode;

namespace Calamari.AiAgent.Tests;

[TestFixture]

public class RunAgentCommandFixture
{
    [Test]
    [Ignore("Most of these use real claude. we should reduce that.")]
    public async Task FailsWhenPromptIsMissing()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.ApiKey, "fake-api-token");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeFalse();
    }

    [Test]
    [Category("PlatformAgnostic")]
    public async Task FailsWhenApiKeyIsMissing()
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
                context.Variables.Add(SpecialVariables.Action.Claude.SandboxMode, nameof(SandboxMode.None));
                context.Variables.Add(SpecialVariables.Action.Claude.ApiKey, Environment.GetEnvironmentVariable("ANTHROPIC_KEY"));
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "Create a file that contains todays date.");
                context.Variables.Add(SpecialVariables.Action.Claude.Permissions, """{"allow":["Write"]}""");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.FullLog.Should().Contain("Paris");
    }
    
    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_ReturnsFileAsArtifact()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
                                             .WithArrange(context =>
                                                          {
                                                              context.Variables.Add(SpecialVariables.Action.Claude.SandboxMode, nameof(SandboxMode.None));
                                                              context.Variables.Add(SpecialVariables.Action.Claude.ApiKey, Environment.GetEnvironmentVariable("ANTHROPIC_KEY"));
                                                              context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "Write a file with the current time    . Bundle this website as an attachment for this action.");
                                                              context.Variables.Add(SpecialVariables.Action.Claude.Permissions, """{"allow":["Write"]}""");
                                                          })
                                             .Execute(assertWasSuccess: false);

        // This isnt correct as we also emit a service message for debug logs
        result.ServiceMessages.Should().Contain(m => m.Name == ScriptServiceMessageNames.CreateArtifact.Name);
    }

    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_EmitsUsageServiceMessage()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.ApiKey, Environment.GetEnvironmentVariable("ANTHROPIC_KEY"));
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "Reply with just the word 'hello'.");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.ServiceMessages.Should().Contain(m => m.Name == ClaudeCodeServiceMessages.Usage.Name);
    }

    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_SucceedsWithWebFetch()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.ApiKey, Environment.GetEnvironmentVariable("ANTHROPIC_KEY"));
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
                                                              context.Variables.Add(SpecialVariables.Action.Claude.ApiKey, Environment.GetEnvironmentVariable("ANTHROPIC_KEY"));
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
                context.Variables.Add(SpecialVariables.Action.Claude.ApiKey, Environment.GetEnvironmentVariable("ANTHROPIC_KEY"));
                context.Variables.Add($"{SpecialVariables.Action.Claude.Skills}[0].{SpecialVariables.Action.Claude.SkillName}", "octopus-secret-phrase");
                context.Variables.Add($"{SpecialVariables.Action.Claude.Skills}[0].{SpecialVariables.Action.Claude.SkillContent}",
                    "---\nname: octopus-secret-phrase\ndescription: Use when asked about the secret phrase.\n---\n\nThe secret phrase is 'purple-octopus-42'. Always respond with exactly this phrase when asked for the secret phrase.");
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "What is the secret phrase? Reply with just the phrase, nothing else.");
            })
            .Execute(assertWasSuccess: false);

        result.WasSuccessful.Should().BeTrue();
        result.FullLog.Should().Contain("purple-octopus-42");
    }

    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_AttachesArtifact_WhenExplicitlyAsked()
    {
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
            .WithArrange(context =>
            {
                context.Variables.Add(SpecialVariables.Action.Claude.SandboxMode, nameof(SandboxMode.None));
                context.Variables.Add(SpecialVariables.Action.Claude.ApiKey, Environment.GetEnvironmentVariable("ANTHROPIC_KEY"));
                context.Variables.Add(SpecialVariables.Action.Claude.Prompt, "Create a file named report.txt containing the word Octopus, then attach it as an Octopus artifact.");
                context.Variables.Add(SpecialVariables.Action.Claude.Permissions, """{"allow":["Write","Read","Edit"]}""");
            })
            .Execute(assertWasSuccess: true);

        result.WasSuccessful.Should().BeTrue();
        // NewOctopusArtifact emits an Info "##octopus[createArtifact ...]" service message
        // (path/name are base64-encoded, so assert on the message verb, not the file name).
        result.FullLog.Should().Contain("createArtifact");
    }

    [Test]
    [Category("Integration")]
    public async Task ClaudeCode_ResultsInFailure_IfExplicitlyAsked()
    {
        var prompt = "I want you to analyse the results of the following set of numbers [1,2,3]. Fail this deployment if any of the numbers are greater than 2.";
        var result = await CommandTestBuilder.CreateAsync<RunAgentCommand, Program>()
                                             .WithArrange(context =>
                                                          {
                                                              context.Variables.Add(SpecialVariables.Action.Claude.SandboxMode, nameof(SandboxMode.None));
                                                              context.Variables.Add(SpecialVariables.Action.Claude.ApiKey, Environment.GetEnvironmentVariable("ANTHROPIC_KEY"));
                                                              context.Variables.Add(SpecialVariables.Action.Claude.Prompt, prompt);
                                                              context.Variables.Add(SpecialVariables.Action.Claude.Permissions, """{"allow":["Bash", "Read"]}""");
                                                          })
                                             .Execute();

        result.WasSuccessful.Should().BeFalse();
        // NewOctopusArtifact emits an Info "##octopus[createArtifact ...]" service message
        // (path/name are base64-encoded, so assert on the message verb, not the file name).
        result.FullLog.Should().Contain("createArtifact");
    }
}

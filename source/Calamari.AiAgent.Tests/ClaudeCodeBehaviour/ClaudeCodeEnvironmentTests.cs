using System.Collections.Generic;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class ClaudeCodeEnvironmentTests
{
    [Test]
    public void Build_PassesAllowlistedVars_AndDropsEverythingElse()
    {
        var source = new Dictionary<string, string>
        {
            ["PATH"] = "/usr/bin",
            ["HOME"] = "/home/octo",
            ["AWS_SECRET_ACCESS_KEY"] = "shh",
            ["RANDOM_VAR"] = "x",
        };

        var env = ClaudeCodeEnvironment.Build(source, [], new Dictionary<string, string>());

        env.Should().ContainKey("PATH");
        env.Should().ContainKey("HOME");
        env.Should().NotContainKey("AWS_SECRET_ACCESS_KEY");
        env.Should().NotContainKey("RANDOM_VAR");
    }

    [Test]
    public void Build_PassesExplicitlyOptedInNames()
    {
        var source = new Dictionary<string, string>
        {
            ["MY_TOOL_HOME"] = "/opt/tool",
            ["SECRET_TOKEN"] = "no",
        };

        var env = ClaudeCodeEnvironment.Build(source, ["MY_TOOL_HOME"], new Dictionary<string, string>());

        env.Should().ContainKey("MY_TOOL_HOME");
        env.Should().NotContainKey("SECRET_TOKEN");
    }

    [Test]
    public void Build_AlwaysSetVars_ArePresentEvenWhenNotAllowlisted()
    {
        // ANTHROPIC_API_KEY isn't allowlisted, so it only reaches the child via alwaysSet.
        var source = new Dictionary<string, string>
        {
            ["PATH"] = "/usr/bin",
            ["ANTHROPIC_API_KEY"] = "leaked-from-worker",
        };

        var env = ClaudeCodeEnvironment.Build(source,
            [],
            new Dictionary<string, string>
            {
                ["ANTHROPIC_API_KEY"] = "fresh",
                ["CLAUDE_CODE_SUBPROCESS_ENV_SCRUB"] = "0",
            });

        env["ANTHROPIC_API_KEY"].Should().Be("fresh");
        env["CLAUDE_CODE_SUBPROCESS_ENV_SCRUB"].Should().Be("0");
    }

    [Test]
    public void Build_AlwaysSet_OverridesAllowlistedSourceValue()
    {
        var source = new Dictionary<string, string>
        {
            ["PATH"] = "/usr/bin",
        };

        var env = ClaudeCodeEnvironment.Build(source,
            [],
            new Dictionary<string, string>
            {
                ["PATH"] = "/controlled/path",
            });

        env["PATH"].Should().Be("/controlled/path");
    }
}
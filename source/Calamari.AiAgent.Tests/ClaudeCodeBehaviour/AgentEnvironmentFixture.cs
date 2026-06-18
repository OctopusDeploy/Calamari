using System.Collections;
using System.Collections.Generic;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class AgentEnvironmentFixture
{
    static Hashtable Source(params (string Key, string Value)[] entries)
    {
        var table = new Hashtable();
        foreach (var (key, value) in entries)
            table[key] = value;
        return table;
    }

    [Test]
    public void Build_PassesAllowlistedVars_AndDropsEverythingElse()
    {
        var source = Source(("PATH", "/usr/bin"), ("HOME", "/home/octo"), ("AWS_SECRET_ACCESS_KEY", "shh"), ("RANDOM_VAR", "x"));

        var env = AgentEnvironment.Build(source, [], new Dictionary<string, string>());

        env.Should().ContainKey("PATH");
        env.Should().ContainKey("HOME");
        env.Should().NotContainKey("AWS_SECRET_ACCESS_KEY");
        env.Should().NotContainKey("RANDOM_VAR");
    }

    [Test]
    public void Build_PassesExplicitlyOptedInNames()
    {
        var source = Source(("MY_TOOL_HOME", "/opt/tool"), ("SECRET_TOKEN", "no"));

        var env = AgentEnvironment.Build(source, ["MY_TOOL_HOME"], new Dictionary<string, string>());

        env.Should().ContainKey("MY_TOOL_HOME");
        env.Should().NotContainKey("SECRET_TOKEN");
    }

    [Test]
    public void Build_PassesLcPrefixedLocaleVars()
    {
        var source = Source(("LC_NUMERIC", "en_US.UTF-8"));

        var env = AgentEnvironment.Build(source, [], new Dictionary<string, string>());

        env.Should().ContainKey("LC_NUMERIC");
    }

    [Test]
    public void Build_AlwaysSetVars_ArePresentAndOverrideInheritedValues()
    {
        // ANTHROPIC_API_KEY on the worker must not leak through; it is replaced by the injected value.
        var source = Source(("PATH", "/usr/bin"), ("ANTHROPIC_API_KEY", "leaked-from-worker"));

        var env = AgentEnvironment.Build(source, [], new Dictionary<string, string>
        {
            ["ANTHROPIC_API_KEY"] = "fresh",
            ["CLAUDE_CODE_SUBPROCESS_ENV_SCRUB"] = "1",
        });

        env["ANTHROPIC_API_KEY"].Should().Be("fresh");
        env["CLAUDE_CODE_SUBPROCESS_ENV_SCRUB"].Should().Be("1");
    }
}

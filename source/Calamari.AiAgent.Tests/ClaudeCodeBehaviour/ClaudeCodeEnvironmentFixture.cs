using System.Collections.Generic;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class ClaudeCodeEnvironmentFixture
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
    public void Build_PassesExplicitLocaleVars_ButDropsOtherLcCategories()
    {
        var source = new Dictionary<string, string>
        {
            ["LC_ALL"] = "en_US.UTF-8",
            ["LC_CTYPE"] = "en_US.UTF-8",
            ["LANG"] = "en_US.UTF-8",
            ["LC_NUMERIC"] = "de_DE.UTF-8",
        };

        var env = ClaudeCodeEnvironment.Build(source, [], new Dictionary<string, string>());

        // The encoding-critical locale vars are explicitly allowlisted; other LC_* categories are not.
        env.Should().ContainKey("LC_ALL");
        env.Should().ContainKey("LC_CTYPE");
        env.Should().ContainKey("LANG");
        env.Should().NotContainKey("LC_NUMERIC");
    }

    [Test]
    public void Build_AlwaysSetVars_ArePresentEvenWhenNotAllowlisted()
    {
        // ANTHROPIC_API_KEY is not allowlisted, so it never inherits from source; alwaysSet injects it directly.
        var source = new Dictionary<string, string>
        {
            ["PATH"] = "/usr/bin",
            ["ANTHROPIC_API_KEY"] = "leaked-from-worker",
        };

        var env = ClaudeCodeEnvironment.Build(source, [], new Dictionary<string, string>
        {
            ["ANTHROPIC_API_KEY"] = "fresh",
            ["CLAUDE_CODE_SUBPROCESS_ENV_SCRUB"] = "1",
        });

        env["ANTHROPIC_API_KEY"].Should().Be("fresh");
        env["CLAUDE_CODE_SUBPROCESS_ENV_SCRUB"].Should().Be("1");
    }

    [Test]
    public void Build_AlwaysSet_OverridesAllowlistedSourceValue()
    {
        // PATH is allowlisted, so it would normally be inherited from source. When it also appears in alwaysSet,
        // the alwaysSet value must win.
        var source = new Dictionary<string, string>
        {
            ["PATH"] = "/usr/bin",
        };

        var env = ClaudeCodeEnvironment.Build(source, [], new Dictionary<string, string>
        {
            ["PATH"] = "/controlled/path",
        });

        env["PATH"].Should().Be("/controlled/path");
    }

    [Test]
    public void Build_PassesProxyVars_FromSource()
    {
        // HTTPS_PROXY (added in the ProxyAndTls segment) must flow through from source so the
        // child process can reach api.anthropic.com when behind a corporate proxy.
        var source = new Dictionary<string, string>
        {
            ["HTTPS_PROXY"] = "http://proxy.corp.example:3128",
            ["SECRET_TOKEN"] = "no",
        };

        var env = ClaudeCodeEnvironment.Build(source, [], new Dictionary<string, string>());

        env.Should().ContainKey("HTTPS_PROXY");
        env["HTTPS_PROXY"].Should().Be("http://proxy.corp.example:3128");
        env.Should().NotContainKey("SECRET_TOKEN");
    }
}

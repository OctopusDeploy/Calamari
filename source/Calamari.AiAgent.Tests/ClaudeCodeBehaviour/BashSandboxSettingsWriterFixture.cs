using System.Linq;
using System.Text.Json;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class BashSandboxSettingsWriterFixture
{
    [Test]
    public void BuildSettings_NoUserEntries_RetainsHardenedDefaults()
    {
        var sandbox = BashSandboxSettingsWriter.BuildSettings(new CalamariVariables()).Sandbox;

        sandbox.Enabled.Should().BeTrue();
        sandbox.FailIfUnavailable.Should().BeTrue();
        sandbox.AllowUnsandboxedCommands.Should().BeFalse();
        sandbox.Network.AllowedDomains.Should().BeEquivalentTo("api.anthropic.com", "statsig.anthropic.com");
        sandbox.Filesystem.AllowWrite.Should().BeEquivalentTo(".", "/tmp");
        sandbox.Filesystem.DenyRead.Should().BeEquivalentTo(
            "~/.ssh", "~/.aws", "~/.azure", "~/.config/gcloud", "~/.kube", "~/.docker",
            "~/.config/gh", "~/.git-credentials", "~/.netrc", "~/.npmrc", "~/.gnupg",
            "~/.claude/.credentials.json");
    }

    [Test]
    public void BuildSettings_AppendsUserEntries_IncludingExcludedCommands()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.BashNetworkAllowedDomains, "example.com");
        vars.Set(SpecialVariables.Action.Claude.BashFilesystemDenyRead, "/etc/secrets");
        vars.Set(SpecialVariables.Action.Claude.BashExcludedCommands, "docker *\ngh *");

        var sandbox = BashSandboxSettingsWriter.BuildSettings(vars).Sandbox;

        sandbox.Network.AllowedDomains.Should().BeEquivalentTo("api.anthropic.com", "statsig.anthropic.com", "example.com");
        sandbox.Filesystem.DenyRead.Should().Contain("/etc/secrets").And.Contain("~/.ssh");
        sandbox.ExcludedCommands.Should().BeEquivalentTo("docker *", "gh *");
    }

    [Test]
    public void BuildSettings_SerializesUnderSandboxKey_CamelCase()
    {
        var json = JsonSerializer.Serialize(BashSandboxSettingsWriter.BuildSettings(new CalamariVariables()), SandboxSettingsJsonContext.Default.BashSandboxSettings);

        using var doc = JsonDocument.Parse(json);
        var sandbox = doc.RootElement.GetProperty("sandbox");
        sandbox.GetProperty("enabled").GetBoolean().Should().BeTrue();
        sandbox.GetProperty("failIfUnavailable").GetBoolean().Should().BeTrue();
        sandbox.GetProperty("allowUnsandboxedCommands").GetBoolean().Should().BeFalse();
        sandbox.GetProperty("filesystem").GetProperty("denyRead").EnumerateArray().Select(e => e.GetString()).Should().Contain("~/.ssh");
    }
}
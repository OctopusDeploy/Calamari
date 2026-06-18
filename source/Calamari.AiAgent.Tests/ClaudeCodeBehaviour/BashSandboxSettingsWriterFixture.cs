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
    public void BuildSettings_AppliesHardenedDefaults_WhenNoUserEntries()
    {
        var sandbox = BashSandboxSettingsWriter.BuildSettings(new CalamariVariables()).Sandbox;

        sandbox.Enabled.Should().BeTrue();
        sandbox.FailIfUnavailable.Should().BeTrue();
        sandbox.AllowUnsandboxedCommands.Should().BeFalse();
        sandbox.Network.AllowedDomains.Should().BeEquivalentTo("api.anthropic.com", "statsig.anthropic.com");
        sandbox.Filesystem.AllowWrite.Should().BeEquivalentTo(".", "/tmp");
        sandbox.Filesystem.DenyRead.Should().Contain("~/.ssh").And.Contain("~/.aws");
    }

    [Test]
    public void BuildSettings_WiresEachVariableToItsList_IncludingExcludedCommands()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.BashNetworkAllowedDomains, "net-allow.example");
        vars.Set(SpecialVariables.Action.Claude.BashNetworkDeniedDomains, "net-deny.example");
        vars.Set(SpecialVariables.Action.Claude.BashFilesystemAllowWrite, "/fs-allow-write");
        vars.Set(SpecialVariables.Action.Claude.BashFilesystemDenyWrite, "/fs-deny-write");
        vars.Set(SpecialVariables.Action.Claude.BashFilesystemDenyRead, "/fs-deny-read");
        vars.Set(SpecialVariables.Action.Claude.BashFilesystemAllowRead, "/fs-allow-read");
        vars.Set(SpecialVariables.Action.Claude.BashExcludedCommands, "docker *\ngh *");

        var sandbox = BashSandboxSettingsWriter.BuildSettings(vars).Sandbox;

        sandbox.Network.AllowedDomains.Should().Contain("net-allow.example").And.Contain("api.anthropic.com");
        sandbox.Network.DeniedDomains.Should().Contain("net-deny.example");
        sandbox.Filesystem.AllowWrite.Should().Contain("/fs-allow-write").And.Contain(".");
        sandbox.Filesystem.DenyWrite.Should().Contain("/fs-deny-write");
        sandbox.Filesystem.DenyRead.Should().Contain("/fs-deny-read").And.Contain("~/.ssh");
        sandbox.Filesystem.AllowRead.Should().Contain("/fs-allow-read");
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

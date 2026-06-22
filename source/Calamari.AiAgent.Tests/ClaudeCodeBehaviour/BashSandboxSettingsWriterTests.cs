using System.Linq;
using System.Text.Json;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class BashSandboxSettingsWriterTests
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
    public void BuildSettings_LeavesOptionalControlsUnset_WhenNoUserEntries()
    {
        var sandbox = BashSandboxSettingsWriter.BuildSettings(new CalamariVariables()).Sandbox;

        sandbox.AutoAllowBashIfSandboxed.Should().BeFalse();
        sandbox.EnableWeakerNestedSandbox.Should().BeNull();
        sandbox.Network.AllowUnixSockets.Should().BeNull();
        sandbox.Network.AllowAllUnixSockets.Should().BeNull();
        sandbox.Network.AllowLocalBinding.Should().BeNull();
        sandbox.Network.HttpProxyPort.Should().BeNull();
        sandbox.Network.SocksProxyPort.Should().BeNull();
    }

    [Test]
    public void BuildSettings_WiresOptionalNetworkAndSandboxControls()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.BashNetworkAllowUnixSockets, "/var/run/one.sock\n/var/run/two.sock");
        vars.Set(SpecialVariables.Action.Claude.BashNetworkAllowAllUnixSockets, "true");
        vars.Set(SpecialVariables.Action.Claude.BashNetworkAllowLocalBinding, "true");
        vars.Set(SpecialVariables.Action.Claude.BashNetworkHttpProxyPort, "8080");
        vars.Set(SpecialVariables.Action.Claude.BashNetworkSocksProxyPort, "1080");
        vars.Set(SpecialVariables.Action.Claude.BashAutoAllowBashIfSandboxed, "true");
        vars.Set(SpecialVariables.Action.Claude.BashEnableWeakerNestedSandbox, "true");

        var sandbox = BashSandboxSettingsWriter.BuildSettings(vars).Sandbox;

        sandbox.Network.AllowUnixSockets.Should().BeEquivalentTo("/var/run/one.sock", "/var/run/two.sock");
        sandbox.Network.AllowAllUnixSockets.Should().BeTrue();
        sandbox.Network.AllowLocalBinding.Should().BeTrue();
        sandbox.Network.HttpProxyPort.Should().Be(8080);
        sandbox.Network.SocksProxyPort.Should().Be(1080);
        sandbox.AutoAllowBashIfSandboxed.Should().BeTrue();
        sandbox.EnableWeakerNestedSandbox.Should().BeTrue();
    }

    [Test]
    public void BuildSettings_OmitsUnsetOptionalControls_FromJson()
    {
        var json = JsonSerializer.Serialize(BashSandboxSettingsWriter.BuildSettings(new CalamariVariables()), SandboxSettingsJsonContext.Default.BashSandboxSettings);

        using var doc = JsonDocument.Parse(json);
        var sandbox = doc.RootElement.GetProperty("sandbox");
        sandbox.TryGetProperty("enableWeakerNestedSandbox", out _).Should().BeFalse();
        sandbox.GetProperty("network").TryGetProperty("allowUnixSockets", out _).Should().BeFalse();
        sandbox.GetProperty("network").TryGetProperty("httpProxyPort", out _).Should().BeFalse();
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

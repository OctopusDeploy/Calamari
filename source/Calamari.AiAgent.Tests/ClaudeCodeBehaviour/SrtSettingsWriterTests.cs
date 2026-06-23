using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class SrtSettingsWriterTests
{
    [Test]
    public void BuildSettings_AppliesSecureDefaults_WhenNoUserEntries()
    {
        var settings = SrtSettingsWriter.BuildSettings(new CalamariVariables());

        settings.Network.AllowedDomains.Should().BeEquivalentTo("api.anthropic.com", "statsig.anthropic.com");
        settings.Filesystem.AllowWrite.Should().BeEquivalentTo(".", "/tmp");
        settings.Filesystem.DenyRead.Should().Contain("~/.ssh").And.Contain("~/.aws");
    }

    [Test]
    public void Write_PlacesSettingsFileAtWorkingDirRoot_NotUnderClaudeConfigDir()
    {
        var workingDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "srt-writer-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var path = SrtSettingsWriter.Write(workingDir, new CalamariVariables());

            path.Should().Be(Path.Combine(workingDir, ".srt-settings.json"));
            File.Exists(path).Should().BeTrue();
            Directory.Exists(Path.Combine(workingDir, ".claude")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(workingDir, recursive: true);
        }
    }

    [Test]
    public void BuildSettings_WiresEachVariableToItsList()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkAllowedDomains, "net-allow.example");
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkDeniedDomains, "net-deny.example");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemAllowWrite, "/fs-allow-write");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemDenyWrite, "/fs-deny-write");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemDenyRead, "/fs-deny-read");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemAllowRead, "/fs-allow-read");

        var settings = SrtSettingsWriter.BuildSettings(vars);

        settings.Network.AllowedDomains.Should().Contain("net-allow.example").And.Contain("api.anthropic.com");
        settings.Network.DeniedDomains.Should().Contain("net-deny.example");
        settings.Filesystem.AllowWrite.Should().Contain("/fs-allow-write").And.Contain(".");
        settings.Filesystem.DenyWrite.Should().Contain("/fs-deny-write");
        settings.Filesystem.DenyRead.Should().Contain("/fs-deny-read").And.Contain("~/.ssh");
        settings.Filesystem.AllowRead.Should().Contain("/fs-allow-read");
    }

    [Test]
    public void BuildSettings_LeavesOptionalControlsUnset_WhenNoUserEntries()
    {
        var settings = SrtSettingsWriter.BuildSettings(new CalamariVariables());

        settings.EnableWeakerNestedSandbox.Should().BeNull();
        settings.Network.AllowUnixSockets.Should().BeNull();
        settings.Network.AllowAllUnixSockets.Should().BeNull();
        settings.Network.AllowLocalBinding.Should().BeNull();
        settings.Network.HttpProxyPort.Should().BeNull();
        settings.Network.SocksProxyPort.Should().BeNull();
    }

    [Test]
    public void BuildSettings_WiresOptionalNetworkAndSandboxControls()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkAllowUnixSockets, "/var/run/one.sock\n/var/run/two.sock");
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkAllowAllUnixSockets, "true");
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkAllowLocalBinding, "true");
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkHttpProxyPort, "8080");
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkSocksProxyPort, "1080");
        vars.Set(SpecialVariables.Action.Claude.SrtEnableWeakerNestedSandbox, "true");

        var settings = SrtSettingsWriter.BuildSettings(vars);

        settings.Network.AllowUnixSockets.Should().BeEquivalentTo("/var/run/one.sock", "/var/run/two.sock");
        settings.Network.AllowAllUnixSockets.Should().BeTrue();
        settings.Network.AllowLocalBinding.Should().BeTrue();
        settings.Network.HttpProxyPort.Should().Be(8080);
        settings.Network.SocksProxyPort.Should().Be(1080);
        settings.EnableWeakerNestedSandbox.Should().BeTrue();
    }

    [Test]
    public void BuildSettings_SerializesToValidCamelCaseJson()
    {
        var json = JsonSerializer.Serialize(SrtSettingsWriter.BuildSettings(new CalamariVariables()), SandboxSettingsJsonContext.Default.SrtSettings);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("network").GetProperty("allowedDomains").EnumerateArray().Select(e => e.GetString()).Should().Contain("api.anthropic.com");
        root.GetProperty("filesystem").GetProperty("denyRead").EnumerateArray().Select(e => e.GetString()).Should().Contain("~/.ssh");
        root.TryGetProperty("enableWeakerNestedSandbox", out _).Should().BeFalse();
        root.GetProperty("network").TryGetProperty("allowUnixSockets", out _).Should().BeFalse();
    }
}

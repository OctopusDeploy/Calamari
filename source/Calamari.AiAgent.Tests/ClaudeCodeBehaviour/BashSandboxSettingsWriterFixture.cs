using System.Linq;
using System.Text.Json;
using Calamari.AiAgent;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class BashSandboxSettingsWriterFixture
{
    static JsonElement Sandbox(JsonElement root) => root.GetProperty("sandbox");

    static string[] FsArray(JsonElement sandbox, string section, string key)
        => sandbox.GetProperty(section).GetProperty(key).EnumerateArray().Select(e => e.GetString()!).ToArray();

    static string[] SandboxArray(JsonElement sandbox, string key)
        => sandbox.GetProperty(key).EnumerateArray().Select(e => e.GetString()!).ToArray();

    [Test]
    public void Baseline_IsEmbeddedAndReadable()
    {
        var baseline = BashSandboxSettingsWriter.LoadBaseline();

        baseline.Should().Contain("api.anthropic.com");
    }

    [Test]
    public void BuildMergedSettings_NoUserEntries_RetainsHardenedDefaults()
    {
        var vars = new CalamariVariables();

        var json = BashSandboxSettingsWriter.BuildMergedSettings(BashSandboxSettingsWriter.LoadBaseline(), vars);

        using var doc = JsonDocument.Parse(json);
        var sandbox = Sandbox(doc.RootElement);
        sandbox.GetProperty("enabled").GetBoolean().Should().BeTrue();
        sandbox.GetProperty("failIfUnavailable").GetBoolean().Should().BeTrue();
        sandbox.GetProperty("allowUnsandboxedCommands").GetBoolean().Should().BeFalse();
        FsArray(sandbox, "network", "allowedDomains").Should().BeEquivalentTo("api.anthropic.com", "api.claude.com", "*.claude.ai");
        FsArray(sandbox, "filesystem", "allowWrite").Should().BeEquivalentTo(".", "/tmp");
        FsArray(sandbox, "filesystem", "denyRead").Should().BeEquivalentTo("~/.ssh", "~/.aws");
    }

    // Mirrors the srt regression: every bash list left blank (UI passes empty-string variables) must
    // still produce valid JSON with the hardened defaults intact.
    [Test]
    public void BuildMergedSettings_AllListsSetToEmptyStrings_ProducesValidJsonWithDefaults()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.BashNetworkAllowedDomains, "");
        vars.Set(SpecialVariables.Action.Claude.BashNetworkDeniedDomains, "");
        vars.Set(SpecialVariables.Action.Claude.BashFilesystemAllowWrite, "");
        vars.Set(SpecialVariables.Action.Claude.BashFilesystemDenyWrite, "");
        vars.Set(SpecialVariables.Action.Claude.BashFilesystemDenyRead, "");
        vars.Set(SpecialVariables.Action.Claude.BashFilesystemAllowRead, "");
        vars.Set(SpecialVariables.Action.Claude.BashExcludedCommands, "");

        var json = BashSandboxSettingsWriter.BuildMergedSettings(BashSandboxSettingsWriter.LoadBaseline(), vars);

        using var doc = JsonDocument.Parse(json);
        var sandbox = Sandbox(doc.RootElement);
        sandbox.GetProperty("enabled").GetBoolean().Should().BeTrue();
        FsArray(sandbox, "network", "allowedDomains").Should().BeEquivalentTo("api.anthropic.com", "api.claude.com", "*.claude.ai");
        FsArray(sandbox, "filesystem", "denyRead").Should().BeEquivalentTo("~/.ssh", "~/.aws");
    }

    [Test]
    public void BuildMergedSettings_AppendsUserEntries_KeepingDefaults()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.BashNetworkAllowedDomains, "example.com\ngithub.com");
        vars.Set(SpecialVariables.Action.Claude.BashFilesystemAllowWrite, "/var/data");
        vars.Set(SpecialVariables.Action.Claude.BashFilesystemDenyRead, "/etc/secrets");
        vars.Set(SpecialVariables.Action.Claude.BashExcludedCommands, "docker *\ngh *");

        var json = BashSandboxSettingsWriter.BuildMergedSettings(BashSandboxSettingsWriter.LoadBaseline(), vars);

        using var doc = JsonDocument.Parse(json);
        var sandbox = Sandbox(doc.RootElement);
        FsArray(sandbox, "network", "allowedDomains")
            .Should().BeEquivalentTo("api.anthropic.com", "api.claude.com", "*.claude.ai", "example.com", "github.com");
        FsArray(sandbox, "filesystem", "allowWrite").Should().BeEquivalentTo(".", "/tmp", "/var/data");
        FsArray(sandbox, "filesystem", "denyRead").Should().BeEquivalentTo("~/.ssh", "~/.aws", "/etc/secrets");
        SandboxArray(sandbox, "excludedCommands").Should().BeEquivalentTo("docker *", "gh *");
    }

    [Test]
    public void BuildMergedSettings_TrimsDropsEmptyLines_AndDedupes()
    {
        var vars = new CalamariVariables();
        // "api.anthropic.com" duplicates a default; "dup.example.com" is repeated; blank lines are dropped.
        vars.Set(SpecialVariables.Action.Claude.BashNetworkAllowedDomains, "  api.anthropic.com  \n\n  dup.example.com \n dup.example.com\n");

        var json = BashSandboxSettingsWriter.BuildMergedSettings(BashSandboxSettingsWriter.LoadBaseline(), vars);

        using var doc = JsonDocument.Parse(json);
        FsArray(Sandbox(doc.RootElement), "network", "allowedDomains")
            .Should().BeEquivalentTo("api.anthropic.com", "api.claude.com", "*.claude.ai", "dup.example.com");
    }
}

using System.Linq;
using System.Text.Json;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class SrtSettingsWriterFixture
{
    [Test]
    public void BuildSettings_NoUserEntries_RetainsSecureDefaults()
    {
        var settings = SrtSettingsWriter.BuildSettings(new CalamariVariables());

        settings.Network.AllowedDomains.Should().BeEquivalentTo("api.anthropic.com", "statsig.anthropic.com");
        settings.Filesystem.AllowWrite.Should().BeEquivalentTo(".", "/tmp");
        settings.Filesystem.DenyRead.Should().BeEquivalentTo(
            "~/.ssh", "~/.aws", "~/.azure", "~/.config/gcloud", "~/.kube", "~/.docker",
            "~/.config/gh", "~/.git-credentials", "~/.netrc", "~/.npmrc", "~/.gnupg",
            "~/.claude/.credentials.json");
    }

    [Test]
    public void BuildSettings_AppendsUserEntries_RetainingDefaults_TrimmedAndDeduped()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkAllowedDomains, "  api.anthropic.com \n\n example.com \n");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemAllowWrite, "/var/data");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemDenyRead, "/etc/secrets");

        var settings = SrtSettingsWriter.BuildSettings(vars);

        settings.Network.AllowedDomains.Should().BeEquivalentTo("api.anthropic.com", "statsig.anthropic.com", "example.com");
        settings.Filesystem.AllowWrite.Should().BeEquivalentTo(".", "/tmp", "/var/data");
        settings.Filesystem.DenyRead.Should().Contain("/etc/secrets").And.Contain("~/.ssh");
    }

    [Test]
    public void BuildSettings_SerializesToValidCamelCaseJson()
    {
        var json = JsonSerializer.Serialize(SrtSettingsWriter.BuildSettings(new CalamariVariables()), SandboxSettingsJsonContext.Default.SrtSettings);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("network").GetProperty("allowedDomains").EnumerateArray().Select(e => e.GetString()).Should().Contain("api.anthropic.com");
        root.GetProperty("filesystem").GetProperty("denyRead").EnumerateArray().Select(e => e.GetString()).Should().Contain("~/.ssh");
    }
}
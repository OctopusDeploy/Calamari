using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Calamari.AiAgent;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class SrtSettingsWriterFixture
{
    static string[] StringArray(JsonElement root, string section, string key)
        => root.GetProperty(section).GetProperty(key).EnumerateArray().Select(e => e.GetString()!).ToArray();

    [Test]
    public void Baseline_IsEmbeddedAndReadable()
    {
        var baseline = SrtSettingsWriter.LoadBaseline();

        baseline.Should().Contain("api.anthropic.com");
    }

    [Test]
    public void BuildMergedSettings_NoUserEntries_RetainsDefaults()
    {
        var vars = new CalamariVariables();

        var json = SrtSettingsWriter.BuildMergedSettings(SrtSettingsWriter.LoadBaseline(), vars);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        StringArray(root, "network", "allowedDomains").Should().BeEquivalentTo("api.anthropic.com", "api.claude.com", "*.claude.ai");
        StringArray(root, "filesystem", "allowWrite").Should().BeEquivalentTo(".", "/tmp");
        StringArray(root, "filesystem", "denyRead").Should().BeEquivalentTo("~/.ssh", "~/.aws");
    }

    // Reproduces the reported deployment failure: the step was configured with every srt list left
    // blank, so the UI passes empty-string variables. The previous JsonNode.ToJsonString(...) path threw
    // "JsonSerializerOptions instance must specify a TypeInfoResolver setting" in the published runtime.
    [Test]
    public void BuildMergedSettings_AllListsSetToEmptyStrings_ProducesValidJsonWithDefaults()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkAllowedDomains, "");
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkDeniedDomains, "");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemAllowWrite, "");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemDenyWrite, "");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemDenyRead, "");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemAllowRead, "");

        var json = SrtSettingsWriter.BuildMergedSettings(SrtSettingsWriter.LoadBaseline(), vars);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        StringArray(root, "network", "allowedDomains").Should().BeEquivalentTo("api.anthropic.com", "api.claude.com", "*.claude.ai");
        StringArray(root, "filesystem", "allowWrite").Should().BeEquivalentTo(".", "/tmp");
        StringArray(root, "filesystem", "denyRead").Should().BeEquivalentTo("~/.ssh", "~/.aws");
    }

    // Documents the root cause of the deployment failure: serializing a JsonNode that holds CLR-string
    // values (as the old MergeList produced via array.Add(...)) requires a TypeInfoResolver. The published
    // Calamari runtime has none (it can't auto-populate one without reflection), producing the exact
    // exception the user saw. SrtSettingsWriter now serializes with a low-level Utf8JsonWriter, which
    // needs no resolver. The throw matching this message is why the JsonNode path is unsafe.
    [Test]
    public void SerializingClrBackedJsonNode_WithoutAResolver_ThrowsTheReportedError()
    {
        var act = () =>
        {
            var node = new JsonObject { ["network"] = new JsonObject { ["allowedDomains"] = new JsonArray { "api.anthropic.com" } } };
            var resolverless = new JsonSerializerOptions { WriteIndented = true };
            resolverless.MakeReadOnly(populateMissingResolver: false);
            return node.ToJsonString(resolverless);
        };

        act.Should().Throw<System.InvalidOperationException>().WithMessage("*TypeInfoResolver*");
    }

    [Test]
    public void BuildMergedSettings_AppendsUserEntries_KeepingDefaults()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkAllowedDomains, "example.com\ngithub.com");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemAllowWrite, "/var/data");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemDenyRead, "/etc/secrets");
        vars.Set(SpecialVariables.Action.Claude.SrtFilesystemAllowRead, "/srv/input");

        var json = SrtSettingsWriter.BuildMergedSettings(SrtSettingsWriter.LoadBaseline(), vars);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        StringArray(root, "network", "allowedDomains")
            .Should().BeEquivalentTo("api.anthropic.com", "api.claude.com", "*.claude.ai", "example.com", "github.com");
        StringArray(root, "filesystem", "allowWrite").Should().BeEquivalentTo(".", "/tmp", "/var/data");
        StringArray(root, "filesystem", "denyRead").Should().BeEquivalentTo("~/.ssh", "~/.aws", "/etc/secrets");
        StringArray(root, "filesystem", "allowRead").Should().BeEquivalentTo("/srv/input");
    }

    [Test]
    public void BuildMergedSettings_TrimsAndDropsEmptyLines()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkDeniedDomains, "  bad.example.com  \n\n   \n evil.example.com\n");

        var json = SrtSettingsWriter.BuildMergedSettings(SrtSettingsWriter.LoadBaseline(), vars);

        using var doc = JsonDocument.Parse(json);
        StringArray(doc.RootElement, "network", "deniedDomains")
            .Should().BeEquivalentTo("bad.example.com", "evil.example.com");
    }

    [Test]
    public void BuildMergedSettings_DedupesAgainstDefaultsAndWithinUserList()
    {
        var vars = new CalamariVariables();
        // "api.anthropic.com" duplicates a default; "dup.example.com" is repeated.
        vars.Set(SpecialVariables.Action.Claude.SrtNetworkAllowedDomains, "api.anthropic.com\ndup.example.com\ndup.example.com");

        var json = SrtSettingsWriter.BuildMergedSettings(SrtSettingsWriter.LoadBaseline(), vars);

        using var doc = JsonDocument.Parse(json);
        StringArray(doc.RootElement, "network", "allowedDomains")
            .Should().BeEquivalentTo("api.anthropic.com", "api.claude.com", "*.claude.ai", "dup.example.com");
    }
}

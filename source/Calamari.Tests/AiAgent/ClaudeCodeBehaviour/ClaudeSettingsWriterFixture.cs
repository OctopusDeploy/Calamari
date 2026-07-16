using System.IO;
using System.Linq;
using System.Text.Json;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class ClaudeSettingsWriterFixture
{
    [Test]
    public void Write_MergesPermissionsAcrossSources_UnioningAllow()
    {
        var fileSystem = Substitute.For<ICalamariFileSystem>();
        var log = Substitute.For<ILog>();

        new ClaudeSettingsWriter(fileSystem, log)
            .Add(new CommandPermissionsSettings("""{"allow":["Read"],"deny":["WebFetch"]}"""))
            .Add(new McpServerPermissionsSettings(new[] { "mcp__octopus__*" }))
            .Write(Path.Combine("work", ".claude", "agent-settings.json"));

        using var doc = JsonDocument.Parse(WrittenSettings(fileSystem));
        var permissions = doc.RootElement.GetProperty("permissions");
        permissions.GetProperty("allow").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo("Read", "mcp__octopus__*");
        permissions.GetProperty("deny").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo("WebFetch");

        log.DidNotReceive().Warn(Arg.Any<string>());
    }

    [Test]
    public void Write_CombinesSandboxAndPermissions_IntoOneFile()
    {
        var fileSystem = Substitute.For<ICalamariFileSystem>();
        var filePath = Path.Combine("work", ".claude", "agent-settings.json");

        var returned = new ClaudeSettingsWriter(fileSystem, Substitute.For<ILog>())
            .Add(new CommandPermissionsSettings("""{"allow":["Read"]}"""))
            .Add(new BashSandboxSettings("""{"sandbox":{"enabled":true}}"""))
            .Write(filePath);

        returned.Should().Be(filePath);
        fileSystem.Received(1).EnsureDirectoryExists(Arg.Any<string>());

        using var doc = JsonDocument.Parse(WrittenSettings(fileSystem));
        doc.RootElement.GetProperty("permissions").GetProperty("allow")
            .EnumerateArray().Select(e => e.GetString()).Should().Contain("Read");
        doc.RootElement.GetProperty("sandbox").GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Test]
    public void Write_LogsWarning_WhenMergeOverwritesAnExistingValue()
    {
        var log = Substitute.For<ILog>();

        new ClaudeSettingsWriter(Substitute.For<ICalamariFileSystem>(), log)
            .Add(new BashSandboxSettings("""{"sandbox":{"enabled":true}}"""))
            .Add(new BashSandboxSettings("""{"sandbox":{"enabled":false}}"""))
            .Write(Path.Combine("work", ".claude", "agent-settings.json"));

        log.Received().Warn(Arg.Is<string>(m => m.Contains("sandbox.enabled")));
    }

    [Test]
    public void HasSettings_IsFalse_UntilSourcesAdded()
    {
        var writer = new ClaudeSettingsWriter(Substitute.For<ICalamariFileSystem>(), Substitute.For<ILog>());
        writer.HasSettings.Should().BeFalse();

        writer.Add(new CommandPermissionsSettings("""{"allow":["Read"]}"""));
        writer.HasSettings.Should().BeTrue();
    }

    static string WrittenSettings(ICalamariFileSystem fileSystem) =>
        (string)fileSystem.ReceivedCalls()
                          .Single(c => c.GetMethodInfo().Name == nameof(ICalamariFileSystem.WriteAllText))
                          .GetArguments()[1];
}

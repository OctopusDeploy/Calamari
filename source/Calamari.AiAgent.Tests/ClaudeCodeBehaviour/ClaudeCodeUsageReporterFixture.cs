using System.Linq;
using System.Text.Json;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Calamari.Contracts.ClaudeCode;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class ClaudeCodeUsageReporterFixture
{
    InMemoryLog log = null!;
    ClaudeCodeUsageReporter reporter = null!;

    [SetUp]
    public void SetUp()
    {
        log = new InMemoryLog();
        reporter = new ClaudeCodeUsageReporter();
    }

    [Test]
    public void NoUsage_DoesNotEmitMessage()
    {
        reporter.WriteServiceMessage(log);

        log.ServiceMessages.Should().NotContain(m => m.Name == ClaudeCodeServiceMessages.Usage.Name);
    }

    [Test]
    public void EmitsSingleMessage_EvenAcrossMultipleSources()
    {
        reporter.AddModelUsage(new[] { new ClaudeCodeModelUsage { Model = "claude-haiku", InputTokens = 10 } });
        reporter.AddModelUsage(new[] { new ClaudeCodeModelUsage { Model = "claude-opus", InputTokens = 20 } });

        reporter.WriteServiceMessage(log);

        log.ServiceMessages.Count(m => m.Name == ClaudeCodeServiceMessages.Usage.Name).Should().Be(1);
    }

    [Test]
    public void SameModelAcrossSources_IsSummedIntoOneEntry()
    {
        // e.g. the injection check and the main agent both use the same model
        reporter.AddModelUsage(new[]
        {
            new ClaudeCodeModelUsage { Model = "claude-haiku", InputTokens = 100, OutputTokens = 50 },
        });
        reporter.AddModelUsage(new[]
        {
            new ClaudeCodeModelUsage
            {
                Model = "claude-haiku",
                InputTokens = 5,
                OutputTokens = 3,
                CacheReadInputTokens = 10,
                CacheCreationInputTokens = 2,
                CostUsd = 0.01,
            },
        });

        reporter.WriteServiceMessage(log);

        var usages = DeserializeModelUsage();
        usages.Should().ContainSingle();
        var haiku = usages.Single();
        haiku.Model.Should().Be("claude-haiku");
        haiku.InputTokens.Should().Be(105);
        haiku.OutputTokens.Should().Be(53);
        haiku.CacheReadInputTokens.Should().Be(10);
        haiku.CacheCreationInputTokens.Should().Be(2);
        haiku.CostUsd.Should().Be(0.01);
    }

    [Test]
    public void DifferentModels_AreKeptAsSeparateEntries()
    {
        reporter.AddModelUsage(new[] { new ClaudeCodeModelUsage { Model = "claude-haiku", InputTokens = 10 } });
        reporter.AddModelUsage(new[] { new ClaudeCodeModelUsage { Model = "claude-opus", InputTokens = 20 } });

        reporter.WriteServiceMessage(log);

        var usages = DeserializeModelUsage();
        usages.Should().HaveCount(2);
        usages.Should().ContainSingle(u => u.Model == "claude-haiku" && u.InputTokens == 10);
        usages.Should().ContainSingle(u => u.Model == "claude-opus" && u.InputTokens == 20);
    }

    [Test]
    public void RunSummaryAttributes_ArePreservedOnTheMessage()
    {
        reporter.SetRunSummary(new System.Collections.Generic.Dictionary<string, string>
        {
            [ClaudeCodeServiceMessages.Usage.CostUsdAttribute] = "0.003000",
            [ClaudeCodeServiceMessages.Usage.NumTurnsAttribute] = "1",
        });
        reporter.AddModelUsage(new[] { new ClaudeCodeModelUsage { Model = "claude-haiku", InputTokens = 10 } });

        reporter.WriteServiceMessage(log);

        var msg = log.ServiceMessages.Single(m => m.Name == ClaudeCodeServiceMessages.Usage.Name);
        msg.GetValue(ClaudeCodeServiceMessages.Usage.CostUsdAttribute).Should().Be("0.003000");
        msg.GetValue(ClaudeCodeServiceMessages.Usage.NumTurnsAttribute).Should().Be("1");
        msg.GetValue(ClaudeCodeServiceMessages.Usage.ModelUsageAttribute).Should().NotBeNull();
    }

    ClaudeCodeModelUsage[] DeserializeModelUsage()
    {
        var msg = log.ServiceMessages.Single(m => m.Name == ClaudeCodeServiceMessages.Usage.Name);
        var json = msg.GetValue(ClaudeCodeServiceMessages.Usage.ModelUsageAttribute);
        return JsonSerializer.Deserialize<ClaudeCodeModelUsage[]>(json!)!;
    }
}

using System;
using System.IO;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class SandboxSettingsWriterTests
{
    const string SettingsBlob = """{"sandbox":{"enabled":true,"network":{"allowedDomains":["api.anthropic.com"]}}}""";

    static CalamariVariables VariablesWith(string settings)
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.SandboxSettings, settings);
        return vars;
    }

    static string CreateWorkingDir() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "sandbox-settings-writer-" + Guid.NewGuid().ToString("N"))).FullName;

    [Test]
    public void WriteBashSettings_WritesBlobVerbatim_ToClaudeSandboxSettingsJson()
    {
        var workingDir = CreateWorkingDir();
        try
        {
            var path = SandboxSettingsWriter.WriteBashSettings(workingDir, VariablesWith(SettingsBlob));

            path.Should().Be(Path.Combine(workingDir, ".claude", "settings.sandbox.json"));
            File.ReadAllText(path).Should().Be(SettingsBlob);
        }
        finally
        {
            Directory.Delete(workingDir, recursive: true);
        }
    }

    [Test]
    public void WriteSandboxRuntimeSettings_WritesBlobVerbatim_ToWorkingDirRoot()
    {
        var workingDir = CreateWorkingDir();
        try
        {
            var path = SandboxSettingsWriter.WriteSandboxRuntimeSettings(workingDir, VariablesWith(SettingsBlob));

            path.Should().Be(Path.Combine(workingDir, ".srt-settings.json"));
            File.ReadAllText(path).Should().Be(SettingsBlob);
            Directory.Exists(Path.Combine(workingDir, ".claude")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(workingDir, recursive: true);
        }
    }

    [Test]
    public void WriteBashSettings_Throws_WhenSettingsMissing()
    {
        var workingDir = CreateWorkingDir();
        try
        {
            var act = () => SandboxSettingsWriter.WriteBashSettings(workingDir, new CalamariVariables());

            act.Should().Throw<CommandException>();
        }
        finally
        {
            Directory.Delete(workingDir, recursive: true);
        }
    }

    [Test]
    public void WriteSandboxRuntimeSettings_Throws_WhenSettingsBlank()
    {
        var workingDir = CreateWorkingDir();
        try
        {
            var act = () => SandboxSettingsWriter.WriteSandboxRuntimeSettings(workingDir, VariablesWith("   "));

            act.Should().Throw<CommandException>();
        }
        finally
        {
            Directory.Delete(workingDir, recursive: true);
        }
    }
}

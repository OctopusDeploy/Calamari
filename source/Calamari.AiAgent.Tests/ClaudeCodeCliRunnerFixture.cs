using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Calamari.AiAgent.Behaviours;
using Calamari.Common.Commands;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests;

[TestFixture]
public class ClaudeCodeCliRunnerFixture
{
    [Test]
    public void SetupSkills_CreatesSkillFile()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-skills-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            ClaudeCodeCliRunner.SetupSkills(workingDir);

            var skillPath = Path.Combine(workingDir, ".claude", "skills", "octopus-deployment.context.md");
            File.Exists(skillPath).Should().BeTrue();

            var content = File.ReadAllText(skillPath);
            content.Should().Contain("name: octopus-deployment-context");
            content.Should().Contain("description:");
            content.Should().Contain("get_deployment_variables");
        }
        finally
        {
            Directory.Delete(workingDir, true);
        }
    }

    [Test]
    public void SetupSkills_WritesUserSkills()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-user-skills-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            var userSkills = new List<UserSkill>
            {
                new() { Name = "my-custom-skill", Content = "---\nname: my-custom-skill\n---\nDo something useful." },
                new() { Name = "another-skill", Content = "---\nname: another-skill\n---\nMore instructions." },
            };

            ClaudeCodeCliRunner.SetupSkills(workingDir, userSkills);

            var skillPath1 = Path.Combine(workingDir, ".claude", "skills", "my-custom-skill.md");
            File.Exists(skillPath1).Should().BeTrue();
            File.ReadAllText(skillPath1).Should().Contain("Do something useful.");

            var skillPath2 = Path.Combine(workingDir, ".claude", "skills", "another-skill.md");
            File.Exists(skillPath2).Should().BeTrue();
            File.ReadAllText(skillPath2).Should().Contain("More instructions.");
        }
        finally
        {
            Directory.Delete(workingDir, true);
        }
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void SanitizeFileName_RejectsEmptyOrWhitespace(string name)
    {
        var act = () => ClaudeCodeCliRunner.SanitizeFileName(name!);
        act.Should().Throw<CommandException>().WithMessage("*cannot be empty*");
    }

    [TestCase("CON")]
    [TestCase("con")]
    [TestCase("NUL")]
    [TestCase("COM1")]
    [TestCase("LPT3")]
    public void SanitizeFileName_RejectsWindowsReservedNames(string name)
    {
        var act = () => ClaudeCodeCliRunner.SanitizeFileName(name);
        act.Should().Throw<CommandException>().WithMessage("*reserved*");
    }

    [Test]
    public void SanitizeFileName_StripsLeadingDots()
    {
        ClaudeCodeCliRunner.SanitizeFileName("...my-skill").Should().Be("my-skill");
    }

    [Test]
    public void SanitizeFileName_ReplacesPathSeparators()
    {
        var result = ClaudeCodeCliRunner.SanitizeFileName("../../etc/passwd");
        result.Should().NotContain("/");
        result.Should().NotContain("\\");
    }

    [Test]
    public void SanitizeFileName_TruncatesLongNames()
    {
        var longName = new string('a', 300);
        ClaudeCodeCliRunner.SanitizeFileName(longName).Length.Should().BeLessOrEqualTo(200);
    }

    [Test]
    public void SetupSkills_SanitizesPathTraversalAttempt()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-traversal-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            var userSkills = new List<UserSkill>
            {
                new() { Name = "../../etc/evil", Content = "content" },
            };

            ClaudeCodeCliRunner.SetupSkills(workingDir, userSkills);

            // The file should be written safely inside the skills directory, not at ../../etc/evil
            var skillsDir = Path.Combine(workingDir, ".claude", "skills");
            var files = Directory.GetFiles(skillsDir, "*.md");
            files.Should().Contain(f => f.Contains("etc-evil"));

            // Verify nothing was written outside
            File.Exists(Path.Combine(workingDir, "..", "..", "etc", "evil.md")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(workingDir, true);
        }
    }

    [Test]
    public void SetupMcpConfig_WritesValidJson_WithServers()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-mcp-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            var servers = new Dictionary<string, McpServerConfig>
            {
                ["github"] = new McpServerConfig
                {
                    Command = "npx",
                    Args = new[] { "-y", "@modelcontextprotocol/server-github" },
                    Env = new Dictionary<string, string> { ["TOKEN"] = "abc123" },
                },
            };

            ClaudeCodeCliRunner.SetupMcpConfig(workingDir, servers);

            var configPath = Path.Combine(workingDir, "mcp-config.json");
            File.Exists(configPath).Should().BeTrue();

            var json = File.ReadAllText(configPath);
            var doc = JsonDocument.Parse(json);
            doc.RootElement.TryGetProperty("mcpServers", out var mcpServers).Should().BeTrue();
            mcpServers.TryGetProperty("github", out var github).Should().BeTrue();
            github.GetProperty("command").GetString().Should().Be("npx");
        }
        finally
        {
            Directory.Delete(workingDir, true);
        }
    }

    [Test]
    public void SetupMcpConfig_WritesEmptyServers_WhenNoneProvided()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-mcp-empty-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            ClaudeCodeCliRunner.SetupMcpConfig(workingDir, new Dictionary<string, McpServerConfig>());

            var configPath = Path.Combine(workingDir, "mcp-config.json");
            var json = File.ReadAllText(configPath);
            var doc = JsonDocument.Parse(json);
            doc.RootElement.TryGetProperty("mcpServers", out var mcpServers).Should().BeTrue();
            mcpServers.EnumerateObject().Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(workingDir, true);
        }
    }
}

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
    public void SetupSkills_CreatesSkillDirectories()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-skills-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            ClaudeCodeCliRunner.SetupSkills(workingDir);

            var skillDir = Path.Combine(workingDir, ".claude", "skills", "octopus-deployment-context");
            Directory.Exists(skillDir).Should().BeTrue();

            var skillMd = Path.Combine(skillDir, "SKILL.md");
            File.Exists(skillMd).Should().BeTrue();

            var content = File.ReadAllText(skillMd);
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

            var skill1 = Path.Combine(workingDir, ".claude", "skills", "my-custom-skill", "SKILL.md");
            File.Exists(skill1).Should().BeTrue();
            File.ReadAllText(skill1).Should().Contain("Do something useful.");

            var skill2 = Path.Combine(workingDir, ".claude", "skills", "another-skill", "SKILL.md");
            File.Exists(skill2).Should().BeTrue();
            File.ReadAllText(skill2).Should().Contain("More instructions.");
        }
        finally
        {
            Directory.Delete(workingDir, true);
        }
    }

    [Test]
    public void SetupSystemPrompt_WritesFile()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-sysprompt-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            var path = ClaudeCodeCliRunner.SetupSystemPrompt(workingDir);

            File.Exists(path).Should().BeTrue();
            File.ReadAllText(path).Should().NotBeEmpty();
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

            // The file should be written safely inside the skills directory
            var skillsDir = Path.Combine(workingDir, ".claude", "skills");
            var dirs = Directory.GetDirectories(skillsDir);
            dirs.Should().Contain(d => Path.GetFileName(d).Contains("etc-evil"));

            // Verify nothing was written outside
            File.Exists(Path.Combine(workingDir, "..", "..", "etc", "evil", "SKILL.md")).Should().BeFalse();
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

            var configPath = ClaudeCodeCliRunner.SetupMcpConfig(workingDir, servers);

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

    [TestCase("simple", "'simple'")]
    [TestCase("has space", "'has space'")]
    [TestCase("it's", @"'it'\''s'")]
    [TestCase("", "''")]
    [TestCase("a'b'c", @"'a'\''b'\''c'")]
    public void ShellQuote_QuotesCorrectly(string input, string expected)
    {
        ClaudeCodeCliRunner.ShellQuote(input).Should().Be(expected);
    }

    [Test]
    public void SetupMcpConfig_WritesEmptyServers_WhenNoneProvided()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-mcp-empty-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            var configPath = ClaudeCodeCliRunner.SetupMcpConfig(workingDir, new Dictionary<string, McpServerConfig>());

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

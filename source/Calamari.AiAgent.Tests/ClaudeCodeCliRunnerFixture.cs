using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Calamari.AiAgent.Behaviours;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests;

[TestFixture]
public class ClaudeCodeCliRunnerFixture
{
    static ClaudeCodeOptions DefaultOptions(string systemPrompt = null, int? maxTurns = null, IReadOnlyList<string> allowedTools = null) =>
        new()
        {
            Prompt = "test prompt",
            ApiToken = "fake-token",
            Model = "claude-sonnet-4-20250514",
            SystemPrompt = systemPrompt,
            MaxTurns = maxTurns,
            AllowedTools = allowedTools ?? new[] { "Read", "Bash" },
        };

    [Test]
    public void BuildArguments_IncludesRequiredFlags()
    {
        var args = ClaudeCodeCliRunner.BuildArguments(DefaultOptions(), "/tmp/work").ToString();

        args.Should().Contain("-p");
        args.Should().Contain("--model claude-sonnet-4-20250514");
        args.Should().Contain("--output-format stream-json");
        args.Should().Contain("--verbose");
        args.Should().Contain("--permission-mode dontAsk");
        args.Should().Contain("--no-session-persistence");
        args.Should().Contain("--strict-mcp-config");
        args.Should().Contain("--mcp-config");
    }

    [Test]
    public void BuildArguments_IncludesAllowedTools()
    {
        var args = ClaudeCodeCliRunner.BuildArguments(DefaultOptions(), "/tmp/work").ToString();

        args.Should().Contain("--allowedTools Read,Bash");
    }

    [Test]
    public void BuildArguments_OmitsAllowedTools_WhenEmpty()
    {
        var options = DefaultOptions(allowedTools: new string[0]);

        var args = ClaudeCodeCliRunner.BuildArguments(options, "/tmp/work").ToString();

        args.Should().NotContain("--allowedTools");
    }

    [Test]
    public void BuildArguments_IncludesMaxTurns_WhenSet()
    {
        var args = ClaudeCodeCliRunner.BuildArguments(DefaultOptions(maxTurns: 5), "/tmp/work").ToString();

        args.Should().Contain("--max-turns 5");
    }

    [Test]
    public void BuildArguments_OmitsMaxTurns_WhenNotSet()
    {
        var args = ClaudeCodeCliRunner.BuildArguments(DefaultOptions(), "/tmp/work").ToString();

        args.Should().NotContain("--max-turns");
    }

    [Test]
    public void BuildArguments_IncludesSystemPrompt_WhenSet()
    {
        var args = ClaudeCodeCliRunner.BuildArguments(DefaultOptions(systemPrompt: "You are helpful"), "/tmp/work").ToString();

        args.Should().Contain("--system-prompt");
        args.Should().Contain("You are helpful");
    }

    [Test]
    public void BuildArguments_OmitsSystemPrompt_WhenNotSet()
    {
        var args = ClaudeCodeCliRunner.BuildArguments(DefaultOptions(), "/tmp/work").ToString();

        args.Should().NotContain("--system-prompt");
    }

    [Test]
    public void BuildArguments_EscapesPromptWithSpaces()
    {
        var options = new ClaudeCodeOptions
        {
            Prompt = "What is the capital of France?",
            ApiToken = "fake",
            Model = "claude-sonnet-4-20250514",
        };

        var args = ClaudeCodeCliRunner.BuildArguments(options, "/tmp/work").ToString();

        args.Should().Contain("\"What is the capital of France?\"");
    }

    [Test]
    public void SetupSkills_CreatesSkillFile()
    {
        var workingDir = Path.Combine(Path.GetTempPath(), $"test-skills-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);

        try
        {
            ClaudeCodeCliRunner.SetupSkills(workingDir);

            var skillPath = Path.Combine(workingDir, ".claude", "skills", "octopus-deployment-context.md");
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

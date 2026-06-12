using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class McpWriterFixture
{
    string workingDir = null!;

    [SetUp]
    public void SetUp()
    {
        workingDir = Path.Combine(Path.GetTempPath(), $"test-mcp-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(workingDir))
            Directory.Delete(workingDir, true);
    }

    [Test]
    public void SetupMcpConfig_WritesValidJson_WithServers()
    {
        var vars = new CalamariVariables();
        var mcpJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                name = "github",
                command = "npx",
                args = new[] { "-y", "@modelcontextprotocol/server-github" },
                env = new Dictionary<string, string> { ["TOKEN"] = "abc123" },
            },
        });
        vars.Set(SpecialVariables.Action.AiAgent.McpServers, mcpJson);

        var configPath = new McpWriter(vars).SetupMcpConfig(workingDir);

        File.Exists(configPath).Should().BeTrue();

        var json = File.ReadAllText(configPath);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("mcpServers", out var mcpServers).Should().BeTrue();
        mcpServers.TryGetProperty("github", out var github).Should().BeTrue();
        github.GetProperty("command").GetString().Should().Be("npx");
    }

    [Test]
    public void SetupMcpConfig_WritesEmptyServers_WhenNoneProvided()
    {
        var configPath = new McpWriter(new CalamariVariables()).SetupMcpConfig(workingDir);

        var json = File.ReadAllText(configPath);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("mcpServers", out var mcpServers).Should().BeTrue();
        mcpServers.EnumerateObject().Should().BeEmpty();
    }

    [Test]
    public void GetAllowedTools_ReturnsMcpWildcardPerServer()
    {
        var vars = new CalamariVariables();
        var mcpJson = JsonSerializer.Serialize(new[]
        {
            new { name = "github", command = "npx" },
            new { name = "slack", command = "npx" },
        });
        vars.Set(SpecialVariables.Action.AiAgent.McpServers, mcpJson);

        var tools = new McpWriter(vars).GetAllowedTools();

        tools.Should().Contain("mcp__github__*");
        tools.Should().Contain("mcp__slack__*");
    }

    [Test]
    public void GetAllowedTools_ReturnsEmpty_WhenNoServersConfigured()
    {
        var tools = new McpWriter(new CalamariVariables()).GetAllowedTools();

        tools.Should().BeEmpty();
    }

    [Test]
    public void SetupMcpConfig_AddsOctopusMcpServer_WhenTokenAndUrlProvided()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.AiAgent.OctopusToken, "API-TESTKEY");
        vars.Set(SpecialVariables.Web.ServerUri, "https://octopus.example.com");

        var configPath = new McpWriter(vars).SetupMcpConfig(workingDir);

        var json = File.ReadAllText(configPath);
        var doc = JsonDocument.Parse(json);
        var mcpServers = doc.RootElement.GetProperty("mcpServers");
        mcpServers.TryGetProperty("octopus", out var octopus).Should().BeTrue();
        octopus.GetProperty("command").GetString().Should().Be("npx");

        var env = octopus.GetProperty("env");
        env.GetProperty("OCTOPUS_SERVER_URL").GetString().Should().Be("https://octopus.example.com");
        env.GetProperty("OCTOPUS_API_KEY").GetString().Should().Be("API-TESTKEY");
    }

    [Test]
    public void SetupMcpConfig_SkipsOctopusMcpServer_WhenTokenMissing()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Web.ServerUri, "https://octopus.example.com");

        var configPath = new McpWriter(vars).SetupMcpConfig(workingDir);

        var json = File.ReadAllText(configPath);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("mcpServers").TryGetProperty("octopus", out _).Should().BeFalse();
    }

    [Test]
    public void SetupMcpConfig_ThrowsOnInvalidMcpJson()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.AiAgent.McpServers, "not valid json {{{");

        var act = () => new McpWriter(vars).SetupMcpConfig(workingDir);

        act.Should().Throw<CommandException>().WithMessage("*Failed to parse*");
    }

    [Test]
    public void SetupMcpConfig_ThrowsWhenServerMissingName()
    {
        var vars = new CalamariVariables();
        var mcpJson = JsonSerializer.Serialize(new[] { new { command = "npx" } });
        vars.Set(SpecialVariables.Action.AiAgent.McpServers, mcpJson);

        var act = () => new McpWriter(vars).SetupMcpConfig(workingDir);

        act.Should().Throw<CommandException>().WithMessage("*must have a name*");
    }

    [Test]
    public void SetupMcpConfig_ThrowsWhenServerMissingCommand()
    {
        var vars = new CalamariVariables();
        var mcpJson = JsonSerializer.Serialize(new[] { new { name = "my-server" } });
        vars.Set(SpecialVariables.Action.AiAgent.McpServers, mcpJson);

        var act = () => new McpWriter(vars).SetupMcpConfig(workingDir);

        act.Should().Throw<CommandException>().WithMessage("*must have a command*");
    }

    [Test]
    public void SetupMcpConfig_InjectsPathEnvVar_WhenNotProvidedByUser()
    {
        var vars = new CalamariVariables();
        var mcpJson = JsonSerializer.Serialize(new[]
        {
            new { name = "test-server", command = "node" },
        });
        vars.Set(SpecialVariables.Action.AiAgent.McpServers, mcpJson);

        var configPath = new McpWriter(vars).SetupMcpConfig(workingDir);

        var json = File.ReadAllText(configPath);
        var doc = JsonDocument.Parse(json);
        var env = doc.RootElement
            .GetProperty("mcpServers")
            .GetProperty("test-server")
            .GetProperty("env");
        env.TryGetProperty("PATH", out _).Should().BeTrue();
    }

    [Test]
    public void SetupMcpConfig_PreservesUserProvidedPathEnvVar()
    {
        var vars = new CalamariVariables();
        var mcpJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                name = "test-server",
                command = "node",
                env = new Dictionary<string, string> { ["PATH"] = "/custom/path" },
            },
        });
        vars.Set(SpecialVariables.Action.AiAgent.McpServers, mcpJson);

        var configPath = new McpWriter(vars).SetupMcpConfig(workingDir);

        var json = File.ReadAllText(configPath);
        var doc = JsonDocument.Parse(json);
        var env = doc.RootElement
            .GetProperty("mcpServers")
            .GetProperty("test-server")
            .GetProperty("env");
        env.GetProperty("PATH").GetString().Should().Be("/custom/path");
    }
}

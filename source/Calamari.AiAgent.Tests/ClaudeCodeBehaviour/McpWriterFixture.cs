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
    public void SetupMcpConfig_WritesStdioAndHttpServers()
    {
        var vars = VariablesWithServers("""
            {
              "type": "stdio",
              "name": "github",
              "command": "npx",
              "args": ["-y", "@modelcontextprotocol/server-github"],
              "env": { "TOKEN": "abc123" },
              "allowedTools": ["get_issue"]
            },
            {
              "type": "http",
              "name": "linear",
              "url": "https://mcp.linear.app/mcp",
              "headers": { "Authorization": "Bearer xyz" },
              "allowedTools": []
            }
            """);

        var config = WriteConfig(vars);

        var github = config.GetProperty("mcpServers").GetProperty("github");
        github.GetProperty("type").GetString().Should().Be("stdio");
        github.GetProperty("command").GetString().Should().Be("npx");
        github.GetProperty("args")[1].GetString().Should().Be("@modelcontextprotocol/server-github");
        github.GetProperty("env").GetProperty("TOKEN").GetString().Should().Be("abc123");

        var linear = config.GetProperty("mcpServers").GetProperty("linear");
        linear.GetProperty("type").GetString().Should().Be("http");
        linear.GetProperty("url").GetString().Should().Be("https://mcp.linear.app/mcp");
        linear.GetProperty("headers").GetProperty("Authorization").GetString().Should().Be("Bearer xyz");
        linear.TryGetProperty("command", out _).Should().BeFalse();
        linear.TryGetProperty("env", out _).Should().BeFalse();
    }

    [Test]
    public void SetupMcpConfig_ParsesPascalCasePayload()
    {
        var vars = new CalamariVariables();
        // `type` should always be lowercase, as it is used as the discriminator and doesn't get to be case-insensitive
        vars.Set(SpecialVariables.Action.Claude.McpServers, """
            [
              { "Name": "github", "Command": "npx", "type": "stdio" }
            ]
            """);

        var config = WriteConfig(vars);

        config.GetProperty("mcpServers").GetProperty("github").GetProperty("command").GetString().Should().Be("npx");
    }

    [Test]
    public void SetupMcpConfig_WritesEmptyServers_WhenNoneProvided()
    {
        var config = WriteConfig(new CalamariVariables());

        config.GetProperty("mcpServers").EnumerateObject().Should().BeEmpty();
    }

    [Test]
    public void GetAllowedTools_PrefixesEachToolWithServerName()
    {
        var vars = VariablesWithServers("""
            {
              "type": "stdio",
              "name": "github",
              "command": "npx",
              "allowedTools": ["get_issue", "create_pull_request"]
            },
            {
              "type": "http",
              "name": "linear",
              "url": "https://mcp.linear.app/mcp",
              "allowedTools": ["*"]
            }
            """);

        var tools = new McpWriter(vars).GetAllowedTools();

        tools.Should().BeEquivalentTo(
            "mcp__github__get_issue",
            "mcp__github__create_pull_request",
            "mcp__linear__*");
    }

    [Test]
    public void GetAllowedTools_ReturnsEmpty_WhenNoToolsSent()
    {
        var vars = VariablesWithServers("""
            { "type": "stdio", "name": "github", "command": "npx" }
            """);

        new McpWriter(vars).GetAllowedTools().Should().BeEmpty();
    }

    [Test]
    public void SetupMcpConfig_AddsOctopusMcpServer_WhenTokenAndUrlProvided()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.OctopusToken, "API-TESTKEY");
        vars.Set(SpecialVariables.Web.ServerUri, "https://octopus.example.com");

        var config = WriteConfig(vars);

        var octopus = config.GetProperty("mcpServers").GetProperty("octopus");
        octopus.GetProperty("command").GetString().Should().Be("npx");

        var env = octopus.GetProperty("env");
        env.GetProperty("OCTOPUS_SERVER_URL").GetString().Should().Be("https://octopus.example.com");
        env.GetProperty("OCTOPUS_API_KEY").GetString().Should().Be("API-TESTKEY");
        
        var tools = new McpWriter(vars).GetAllowedTools();

        tools.Should().BeEquivalentTo("mcp__octopus__*");
    }

    [Test]
    public void SetupMcpConfig_SkipsOctopusMcpServer_WhenTokenMissing()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Web.ServerUri, "https://octopus.example.com");

        var config = WriteConfig(vars);

        config.GetProperty("mcpServers").TryGetProperty("octopus", out _).Should().BeFalse();
    }

    [Test]
    public void SetupMcpConfig_ThrowsOnInvalidMcpJson()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.McpServers, "not valid json {{{");

        var act = () => new McpWriter(vars).SetupMcpConfig(workingDir);

        act.Should().Throw<CommandException>().WithMessage("*Failed to parse*");
    }

    [Test]
    public void SetupMcpConfig_ThrowsOnUnrecognisedServerType()
    {
        var vars = VariablesWithServers("""
            { "type": "websocket", "name": "github" }
            """);

        var act = () => new McpWriter(vars).SetupMcpConfig(workingDir);

        act.Should().Throw<CommandException>().WithMessage("*Failed to parse*");
    }

    [Test]
    public void SetupMcpConfig_ThrowsWhenTypeDiscriminatorMissing()
    {
        var vars = VariablesWithServers("""
            { "name": "github", "command": "npx" }
            """);

        var act = () => new McpWriter(vars).SetupMcpConfig(workingDir);

        act.Should().Throw<CommandException>().WithMessage("*Failed to parse*");
    }

    [Test]
    public void SetupMcpConfig_ThrowsWhenServerMissingRequiredProperties()
    {
        var vars = VariablesWithServers("""
            { "type": "stdio", "name": "my-server" }
            """);

        var act = () => new McpWriter(vars).SetupMcpConfig(workingDir);

        act.Should().Throw<CommandException>().WithMessage("*Failed to parse*Command*");
    }

    [Test]
    public void SetupMcpConfig_ThrowsWhenServerNameBlank()
    {
        var vars = VariablesWithServers("""
            { "type": "stdio", "name": "", "command": "npx" }
            """);

        var act = () => new McpWriter(vars).SetupMcpConfig(workingDir);

        act.Should().Throw<CommandException>().WithMessage("*must have a name*");
    }

    [Test]
    public void SetupMcpConfig_ThrowsWhenHttpServerUrlBlank()
    {
        var vars = VariablesWithServers("""
            { "type": "http", "name": "my-server", "url": "" }
            """);

        var act = () => new McpWriter(vars).SetupMcpConfig(workingDir);

        act.Should().Throw<CommandException>().WithMessage("*'my-server' must have a URL*");
    }

    [Test]
    public void SetupMcpConfig_ThrowsOnDuplicateServerNames()
    {
        var vars = VariablesWithServers("""
            { "type": "stdio", "name": "github", "command": "npx" },
            { "type": "http", "name": "github", "url": "https://example.com/mcp" }
            """);

        var act = () => new McpWriter(vars).SetupMcpConfig(workingDir);

        act.Should().Throw<CommandException>().WithMessage("*Duplicate MCP server names: github*");
    }

    [Test]
    public void SetupMcpConfig_InjectsPathEnvVar_WhenNotProvided()
    {
        var vars = VariablesWithServers("""
            { "type": "stdio", "name": "test-server", "command": "node" }
            """);

        var config = WriteConfig(vars);

        var env = config.GetProperty("mcpServers").GetProperty("test-server").GetProperty("env");
        env.TryGetProperty("PATH", out _).Should().BeTrue();
    }

    [Test]
    public void SetupMcpConfig_PreservesProvidedPathEnvVar()
    {
        var vars = VariablesWithServers("""
            {
              "type": "stdio",
              "name": "test-server",
              "command": "node",
              "env": { "PATH": "/custom/path" }
            }
            """);

        var config = WriteConfig(vars);

        var env = config.GetProperty("mcpServers").GetProperty("test-server").GetProperty("env");
        env.GetProperty("PATH").GetString().Should().Be("/custom/path");
    }

    static CalamariVariables VariablesWithServers(string serversJson)
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.McpServers, $"[{serversJson}]");
        return vars;
    }

    JsonElement WriteConfig(CalamariVariables vars)
    {
        var configPath = new McpWriter(vars).SetupMcpConfig(workingDir);
        File.Exists(configPath).Should().BeTrue();
        return JsonDocument.Parse(File.ReadAllText(configPath)).RootElement.Clone();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public void BuildServerSpecs_IncludesCustomServers()
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
        vars.Set(SpecialVariables.Action.Claude.McpServers, mcpJson);

        var specs = new McpWriter(vars).BuildServerSpecs();

        var github = specs.Single(s => s.Name == "github");
        github.Command.Should().Be("npx");
        github.Args.Should().Equal("-y", "@modelcontextprotocol/server-github");
        github.Env!["TOKEN"].Should().Be("abc123");
    }

    [Test]
    public void BuildServerSpecs_IncludesOctopusServer_WhenTokenAndUrlProvided()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.OctopusToken, "API-TESTKEY");
        vars.Set(SpecialVariables.Web.ServerUri, "https://octopus.example.com");

        var octopus = new McpWriter(vars).BuildServerSpecs().Single(s => s.Name == "octopus");

        octopus.Command.Should().Be("npx");
        octopus.Args.Should().Contain("@octopusdeploy/mcp-server");
        // The secret rides in the spec so the broker can hand it to the child process — never to disk.
        octopus.Env!["OCTOPUS_SERVER_URL"].Should().Be("https://octopus.example.com");
        octopus.Env!["OCTOPUS_API_KEY"].Should().Be("API-TESTKEY");
    }

    [Test]
    public void BuildServerSpecs_OmitsOctopusServer_WhenTokenMissing()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Web.ServerUri, "https://octopus.example.com");

        var specs = new McpWriter(vars).BuildServerSpecs();

        specs.Should().NotContain(s => s.Name == "octopus");
    }

    [Test]
    public void BuildServerSpecs_ThrowsOnInvalidMcpJson()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.McpServers, "not valid json {{{");

        var act = () => new McpWriter(vars).BuildServerSpecs();

        act.Should().Throw<CommandException>().WithMessage("*Failed to parse*");
    }

    [Test]
    public void BuildServerSpecs_ThrowsWhenServerNameBlank()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.McpServers, JsonSerializer.Serialize(new[] { new { name = "", command = "npx" } }));

        var act = () => new McpWriter(vars).BuildServerSpecs();

        act.Should().Throw<CommandException>().WithMessage("*must have a name*");
    }

    [Test]
    public void BuildServerSpecs_ThrowsWhenServerCommandBlank()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.McpServers, JsonSerializer.Serialize(new[] { new { name = "my-server", command = "" } }));

        var act = () => new McpWriter(vars).BuildServerSpecs();

        act.Should().Throw<CommandException>().WithMessage("*must have a command*");
    }

    [Test]
    public void GetAllowedTools_ReturnsMcpWildcardPerServer()
    {
        var vars = new CalamariVariables();
        vars.Set(SpecialVariables.Action.Claude.McpServers, JsonSerializer.Serialize(new[]
        {
            new { name = "github", command = "npx" },
            new { name = "slack", command = "npx" },
        }));

        var writer = new McpWriter(vars);
        var tools = writer.GetAllowedTools(writer.BuildServerSpecs());

        tools.Should().Contain("mcp__github__*");
        tools.Should().Contain("mcp__slack__*");
    }

    [Test]
    public void WriteConfig_WritesSecretFreeHttpEntries()
    {
        // mcp-config.json only references the broker's loopback endpoints — never a command, env, or token.
        var endpoints = new Dictionary<string, Uri>
        {
            ["octopus"] = new Uri("http://127.0.0.1:54321/"),
            ["github"] = new Uri("http://127.0.0.1:54322/"),
        };

        var configPath = new McpWriter(new CalamariVariables()).WriteConfig(workingDir, endpoints);

        var json = File.ReadAllText(configPath);
        var mcpServers = JsonDocument.Parse(json).RootElement.GetProperty("mcpServers");

        var octopus = mcpServers.GetProperty("octopus");
        octopus.GetProperty("type").GetString().Should().Be("http");
        octopus.GetProperty("url").GetString().Should().Be("http://127.0.0.1:54321/");
        octopus.TryGetProperty("command", out _).Should().BeFalse();
        octopus.TryGetProperty("env", out _).Should().BeFalse();
        mcpServers.GetProperty("github").GetProperty("url").GetString().Should().Be("http://127.0.0.1:54322/");

        json.Should().NotContain("OCTOPUS_API_KEY");
    }
}

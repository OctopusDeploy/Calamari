#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Testing.Helpers;
using FluentAssertions;
using ModelContextProtocol.Client;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

/// <summary>
/// Spike verification (MD-2096): proves the broker mechanism end-to-end without any Octopus token or
/// Claude — it spawns a public stub MCP server as a child, re-exposes it over loopback HTTP, and an
/// HTTP MCP client lists and calls tools through it. Explicit because it runs `npx` (needs Node and a
/// one-time package fetch); not part of normal CI.
/// </summary>
[TestFixture]
[Explicit("Spike smoke test: requires Node/npx and network to fetch @modelcontextprotocol/server-everything.")]
[Category("Integration")]
public class McpBrokerSmokeTest
{
    [Test]
    public async Task Broker_ProxiesAStdioMcpServer_OverLoopbackHttp()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var log = new InMemoryLog();

        // The Octopus server is just one spec; here we broker a generic stub MCP server the same way.
        var spec = new McpServerSpec
        {
            Name = "everything",
            Command = "npx",
            Args = new[] { "-y", "@modelcontextprotocol/server-everything" },
        };

        await using var broker = await McpBroker.StartAsync(new[] { spec }, log, cts.Token);

        // The agent-facing endpoint is loopback only — the secret-free hop.
        var endpoint = broker.Endpoints["everything"];
        endpoint.Host.Should().Be("127.0.0.1");

        // Connect as the agent would: an HTTP MCP client against the broker endpoint (no secret on this hop).
        var clientTransport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Name = "smoke-test-client",
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
        });
        await using var client = await McpClient.CreateAsync(clientTransport, cancellationToken: cts.Token);

        // tools/list is forwarded from the upstream child through the broker.
        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);
        tools.Should().Contain(t => t.Name == "echo", "the stub server's tools must be visible through the broker");

        // tools/call round-trips: the broker forwards the call to the child and returns its result verbatim.
        var result = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object?> { ["message"] = "broker-works" },
            cancellationToken: cts.Token);

        var text = string.Concat(result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(c => c.Text));
        text.Should().Contain("broker-works");
    }
}

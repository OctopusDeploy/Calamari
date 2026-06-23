using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

/// <summary>
/// An MCP server for the broker to front: the command Calamari spawns and the secrets it holds.
/// This is also the shape of each entry in the user's McpServers variable, deserialized directly.
/// </summary>
public record McpServerSpec
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public IReadOnlyList<string>? Args { get; init; }
    public IReadOnlyDictionary<string, string?>? Env { get; init; }
}

/// <summary>
/// In-process credential broker for MCP servers (MD-2096 spike).
///
/// Calamari spawns each MCP server as its OWN child — the server's secrets live only in that child's
/// environment — and re-exposes it to the agent over a secret-free loopback HTTP endpoint. No MCP
/// secret ever enters Claude Code's environment, disk, or descendants; the agent reaches each server
/// sideways through the broker, which is a transparent proxy:
///   upstream   = an MCP client over stdio that holds the real secrets;
///   downstream = an MCP server over loopback HTTP that forwards tools/list and tools/call.
///
/// One loopback host is started per brokered server (the count is small for a deployment step); the
/// two forwarding handlers are the entire proxy and the audit chokepoint.
/// </summary>
public sealed class McpBroker : IAsyncDisposable
{
    readonly IReadOnlyList<BrokeredServer> servers;

    McpBroker(IReadOnlyList<BrokeredServer> servers)
    {
        this.servers = servers;
        Endpoints = servers.ToDictionary(server => server.Name, server => server.Endpoint);
    }

    /// <summary>The loopback URL for each brokered server, keyed by server name (the agent's mcp-config entries).</summary>
    public IReadOnlyDictionary<string, Uri> Endpoints { get; }

    public static async Task<McpBroker> StartAsync(IReadOnlyList<McpServerSpec> specs, ILog log, CancellationToken cancellationToken)
    {
        var started = new List<BrokeredServer>();
        try
        {
            foreach (var spec in specs)
                started.Add(await BrokeredServer.StartAsync(spec, log, cancellationToken));

            return new McpBroker(started);
        }
        catch
        {
            // Tear down anything already started so a failure part-way through leaves no orphaned child.
            for (var i = started.Count - 1; i >= 0; i--)
                await started[i].DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        for (var i = servers.Count - 1; i >= 0; i--)
            await servers[i].DisposeAsync();
    }

    /// <summary>One brokered server: its upstream stdio client and its downstream loopback HTTP host.</summary>
    sealed class BrokeredServer : IAsyncDisposable
    {
        readonly McpClient upstream;
        readonly WebApplication app;

        BrokeredServer(string name, McpClient upstream, WebApplication app, Uri endpoint)
        {
            Name = name;
            this.upstream = upstream;
            this.app = app;
            Endpoint = endpoint;
        }

        public string Name { get; }
        public Uri Endpoint { get; }

        public static async Task<BrokeredServer> StartAsync(McpServerSpec spec, ILog log, CancellationToken cancellationToken)
        {
            // Upstream: spawn the MCP server as a Calamari child holding its secrets. We don't inherit
            // the parent environment — the secrets are the point of the broker — so the child gets only
            // the spec's env plus the minimal non-secret system variables it needs to launch.
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = $"{spec.Name}-upstream",
                Command = spec.Command,
                Arguments = spec.Args?.ToArray(),
                InheritEnvironmentVariables = false,
                EnvironmentVariables = BuildChildEnvironment(spec.Env),
                StandardErrorLines = line => log.Verbose($"[mcp:{spec.Name}] {line}"),
            });

            var upstream = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);

            try
            {
                // Readiness/health check: one round-trip proves the child started and accepted its
                // secrets (covers process cold-start and any non-empty-credential startup requirement).
                await upstream.ListToolsAsync(cancellationToken: cancellationToken);

                // Downstream: loopback HTTP MCP server on an OS-assigned ephemeral port. The two handlers
                // forward verbatim to the upstream client and are the audit chokepoint.
                var builder = WebApplication.CreateSlimBuilder();
                // Bind to an OS-assigned ephemeral port on the IPv4 loopback. ListenLocalhost does not
                // support dynamic ports, so bind the loopback address explicitly.
                builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));

                builder.Services
                       .AddMcpServer()
                       .WithHttpTransport()
                       .WithListToolsHandler(async (_, ct) =>
                       {
                           var tools = await upstream.ListToolsAsync(cancellationToken: ct);
                           // Forward the upstream protocol tools verbatim (preserves names + input schemas).
                           return new ListToolsResult { Tools = tools.Select(tool => tool.ProtocolTool).ToList() };
                       })
                       .WithCallToolHandler(async (callContext, ct) =>
                       {
                           var request = callContext.Params!;
                           log.Verbose($"MCP tool call [{spec.Name}]: {request.Name}"); // audit point
                           var arguments = request.Arguments?.ToDictionary(arg => arg.Key, arg => (object?)arg.Value);
                           // CallToolResult is the same type on both sides — pass the upstream result through.
                           return await upstream.CallToolAsync(request.Name, arguments, cancellationToken: ct);
                       });

                var app = builder.Build();
                app.MapMcp();
                await app.StartAsync(cancellationToken);

                var endpoint = ResolveLoopbackEndpoint(app);
                log.Verbose($"MCP broker for '{spec.Name}' listening on {endpoint}");
                return new BrokeredServer(spec.Name, upstream, app, endpoint);
            }
            catch
            {
                await upstream.DisposeAsync();
                throw;
            }
        }

        static Dictionary<string, string?> BuildChildEnvironment(IReadOnlyDictionary<string, string?>? specEnv)
        {
            var environment = new Dictionary<string, string?>();
            if (specEnv != null)
            {
                foreach (var kvp in specEnv)
                    environment[kvp.Key] = kvp.Value;
            }

            // Because we don't inherit the parent environment, hand the child the minimal non-secret
            // system variables a launcher (e.g. npx/node) needs to start and resolve its cache cross-platform,
            // plus the HTTP proxy routing variables a proxied worker needs to reach the npm registry and the
            // Octopus Server (the Octopus MCP server's HTTP client honours these). These are routing config,
            // not secrets.
            string[] passThrough =
            {
                "PATH", "Path", "HOME", "USERPROFILE", "APPDATA", "LOCALAPPDATA",
                "SystemRoot", "windir", "TEMP", "TMP", "PATHEXT", "ComSpec",
                "ProgramFiles", "ProgramFiles(x86)",
                "HTTP_PROXY", "HTTPS_PROXY", "NO_PROXY",
                "http_proxy", "https_proxy", "no_proxy",
            };
            foreach (var name in passThrough)
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (value != null && !environment.ContainsKey(name))
                    environment[name] = value;
            }

            return environment;
        }

        static Uri ResolveLoopbackEndpoint(WebApplication app)
        {
            var address = app.Urls.FirstOrDefault()
                          ?? throw new InvalidOperationException("MCP broker did not bind to any address.");
            // Normalise to an explicit IPv4 loopback URL regardless of how Kestrel reports the bound address.
            var bound = new Uri(address);
            return new UriBuilder(Uri.UriSchemeHttp, "127.0.0.1", bound.Port).Uri;
        }

        public async ValueTask DisposeAsync()
        {
            // Stop the listener first, then terminate the upstream stdio child (kills the MCP server process).
            try
            {
                using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await app.StopAsync(stopTimeout.Token);
            }
            catch
            {
                // Best-effort shutdown — disposal must not mask the original outcome of the run.
            }

            await app.DisposeAsync();
            await upstream.DisposeAsync();
        }
    }
}

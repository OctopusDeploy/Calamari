using System.Text.Json.Serialization;

namespace Octopus.Calamari.Contracts.ClaudeCode;

public class McpServers(McpServer[] Servers);

public abstract class McpServer(string name, IReadOnlyDictionary<string, string> env, IReadOnlyCollection<string> allowedTools)
{
    public string Name { get; init; } = name;
    public virtual string Type { get; init; }
    public IReadOnlyDictionary<string, string> Env { get; init; } = env;
    public IReadOnlyCollection<string> AllowedTools { get; init; } = allowedTools;
}

public class StdioMcpServer(
    string name,
    IReadOnlyDictionary<string, string> env,
    IReadOnlyCollection<string> allowedTools,
    string command,
    IReadOnlyList<string> args) : McpServer(name, env, allowedTools)
{
    public const string DiscriminatorValue = "stdio";
    public override string Type => DiscriminatorValue;
    
    public string Command { get; init; } = command;
    public IReadOnlyList<string> Args { get; init; } = args;
}

public class HttpMcpServer(
    string name,
    IReadOnlyDictionary<string, string> env,
    IReadOnlyCollection<string> allowedTools,
    string url,
    IReadOnlyDictionary<string, string> headers) : McpServer(name, env, allowedTools)
{
    public const string DiscriminatorValue = "http";
    public override string Type => DiscriminatorValue;
    public string Url { get; init; } = url;
    public IReadOnlyDictionary<string, string> Headers { get; init; } = headers;
}
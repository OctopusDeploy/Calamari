using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public sealed class SandboxNetwork
{
    public List<string> AllowedDomains { get; set; } = [];
    public List<string> DeniedDomains { get; set; } = [];
}

public sealed class SandboxFilesystem
{
    public List<string> AllowWrite { get; set; } = [];
    public List<string> DenyWrite { get; set; } = [];
    public List<string> DenyRead { get; set; } = [];
    public List<string> AllowRead { get; set; } = [];
}

public sealed class SrtSettings
{
    public SandboxNetwork Network { get; set; } = new();
    public SandboxFilesystem Filesystem { get; set; } = new();
}

public sealed class BashSandbox
{
    public bool Enabled { get; set; } = true;
    public bool FailIfUnavailable { get; set; } = true;
    public bool AllowUnsandboxedCommands { get; set; }
    public SandboxNetwork Network { get; set; } = new();
    public SandboxFilesystem Filesystem { get; set; } = new();
    public List<string> ExcludedCommands { get; set; } = [];
}

public sealed class BashSandboxSettings
{
    public BashSandbox Sandbox { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SrtSettings))]
[JsonSerializable(typeof(BashSandboxSettings))]
partial class SandboxSettingsJsonContext : JsonSerializerContext;
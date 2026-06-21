using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public sealed class SandboxNetwork
{
    public List<string> AllowedDomains { get; set; } = [];
    public List<string> DeniedDomains { get; set; } = [];

    public List<string>? AllowUnixSockets { get; set; }
    public bool? AllowAllUnixSockets { get; set; }
    public bool? AllowLocalBinding { get; set; }

    public int? HttpProxyPort { get; set; }
    public int? SocksProxyPort { get; set; }
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
    // Configurable
    public SandboxNetwork Network { get; set; } = new();
    public SandboxFilesystem Filesystem { get; set; } = new();
    public bool? EnableWeakerNestedSandbox { get; set; }
}

public sealed class BashSandbox
{
    // Not configurable
    public bool Enabled { get; set; } = true;
    public bool FailIfUnavailable { get; set; } = true;
    public bool AllowUnsandboxedCommands { get; set; } = false;
    public bool? AutoAllowBashIfSandboxed { get; set; } = false;

    // Configurable
    public SandboxNetwork Network { get; set; } = new();
    public SandboxFilesystem Filesystem { get; set; } = new();
    public List<string> ExcludedCommands { get; set; } = [];
    public bool? EnableWeakerNestedSandbox { get; set; }
}

public sealed class BashSandboxSettings
{
    public BashSandbox Sandbox { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SrtSettings))]
[JsonSerializable(typeof(BashSandboxSettings))]
partial class SandboxSettingsJsonContext : JsonSerializerContext;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public sealed class SandboxNetwork
{
    public IReadOnlyList<string> AllowedDomains { get; init; } = [];
    public IReadOnlyList<string> DeniedDomains { get; init; } = [];

    public IReadOnlyList<string>? AllowUnixSockets { get; init; }
    public bool? AllowAllUnixSockets { get; init; }
    public bool? AllowLocalBinding { get; init; }

    public int? HttpProxyPort { get; init; }
    public int? SocksProxyPort { get; init; }
}

public sealed class SandboxFilesystem
{
    public IReadOnlyList<string> AllowWrite { get; init; } = [];
    public IReadOnlyList<string> DenyWrite { get; init; } = [];
    public IReadOnlyList<string> DenyRead { get; init; } = [];
    public IReadOnlyList<string> AllowRead { get; init; } = [];
}

public sealed class SrtSettings
{
    // Configurable
    public SandboxNetwork Network { get; init; } = new();
    public SandboxFilesystem Filesystem { get; init; } = new();
    public bool? EnableWeakerNestedSandbox { get; init; }
}

public sealed class BashSandbox
{
    // Not configurable
    public bool Enabled { get; init; } = true;
    public bool FailIfUnavailable { get; init; } = true;
    public bool AllowUnsandboxedCommands { get; init; } = false;

    // Configurable
    public SandboxNetwork Network { get; init; } = new();
    public SandboxFilesystem Filesystem { get; init; } = new();
    public IReadOnlyList<string> ExcludedCommands { get; init; } = [];
    public bool? AutoAllowBashIfSandboxed { get; init; } = false;
    public bool? EnableWeakerNestedSandbox { get; init; }
}

public sealed class BashSandboxSettings
{
    public BashSandbox Sandbox { get; init; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SrtSettings))]
[JsonSerializable(typeof(BashSandboxSettings))]
partial class SandboxSettingsJsonContext : JsonSerializerContext;

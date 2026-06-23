using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public static class ClaudeCodeEnvironment
{
    public static Dictionary<string, string> Build(
        IDictionary<string, string> source,
        IEnumerable<string> additionalAllowedNames,
        IDictionary<string, string> alwaysSet)
    {
        var allowed = new HashSet<string>(DefaultAllowList, StringComparer.OrdinalIgnoreCase);
        allowed.UnionWith(additionalAllowedNames.WhereNotNullOrWhiteSpace().ToHashSet(StringComparer.OrdinalIgnoreCase));

        var result = source
                     .Where(entry => !string.IsNullOrEmpty(entry.Key) && allowed.Contains(entry.Key))
                     .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        foreach (var kvp in alwaysSet)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    public static Dictionary<string, string> GetCurrentEnvironmentVariables()
        => Environment.GetEnvironmentVariables()
                      .Cast<DictionaryEntry>()
                      .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty, StringComparer.Ordinal);

    // Allowlist segments
    // Locating and launching executables (PATH everywhere, ComSpec/PATHEXT resolve commands on Windows).
    static readonly string[] ExecutableResolution = ["PATH", "ComSpec", "PATHEXT"];

    // Current user identity and home directory (HOME on *nix, USERPROFILE on Windows).
    static readonly string[] UserAndHome = ["HOME", "USERPROFILE", "USER", "USERNAME", "LOGNAME"];

    // Shell and terminal type, so spawned shells and terminal-aware tools behave correctly.
    static readonly string[] ShellAndTerminal = ["SHELL", "TERM"];

    // Locale, character encoding, and timezone — keeps subprocess text handling (UTF-8) and times correct.
    static readonly string[] LocaleAndTime = ["LANG", "LANGUAGE", "LC_ALL", "LC_CTYPE", "TZ"];

    // Temporary directories that tools write scratch files to.
    static readonly string[] TemporaryDirectories = ["TMPDIR", "TMP", "TEMP"];

    // XDG base directories — where Linux tools resolve config, cache, data, and runtime files.
    static readonly string[] XdgBaseDirectories = ["XDG_RUNTIME_DIR", "XDG_CACHE_HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME"];

    // Windows system and application directories that many Windows programs require to start.
    static readonly string[] WindowsSystemDirectories =
    [
        "SystemRoot",
        "windir",
        "SystemDrive",
        "APPDATA",
        "LOCALAPPDATA",
        "ProgramData",
        "ProgramFiles",
        "ProgramFiles(x86)",
        "CommonProgramFiles",
    ];

    // Machine and CPU information some tools read to size their work.
    static readonly string[] MachineInfo = ["NUMBER_OF_PROCESSORS", "PROCESSOR_ARCHITECTURE", "OS"];

    // Proxy configuration and custom CA trust.
    static readonly string[] ProxyAndTls =
    [
        "ANTHROPIC_BASE_URL",
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "NO_PROXY",
        "http_proxy",
        "https_proxy",
        "no_proxy",
        "NODE_EXTRA_CA_CERTS",
        "SSL_CERT_FILE",
        "SSL_CERT_DIR",
    ];

    static readonly string[] DefaultAllowList =
    [
        .. ExecutableResolution,
        .. UserAndHome,
        .. ShellAndTerminal,
        .. LocaleAndTime,
        .. TemporaryDirectories,
        .. XdgBaseDirectories,
        .. WindowsSystemDirectories,
        .. MachineInfo,
        .. ProxyAndTls,
    ];
}
using System;
using System.Collections;
using System.Collections.Generic;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

// Builds the environment passed to the agent process. The worker environment is NOT inherited
// wholesale (it is the main carrier of injected secrets); only an allowlist of names needed for
// claude/srt/node and the PTY helpers to run is passed through, plus any names the step
// explicitly opts in, plus the vars we always set (e.g. the freshly-injected API token).
public static class AgentEnvironment
{
    // Names always passed through from the worker so claude/srt/node and `script`/`su` work.
    static readonly string[] DefaultAllowList =
    [
        "PATH", "HOME", "USER", "USERNAME", "LOGNAME", "SHELL", "TERM", "TZ",
        "LANG", "LANGUAGE", "LC_ALL", "LC_CTYPE", "TMPDIR", "TMP", "TEMP",
        "XDG_RUNTIME_DIR", "XDG_CACHE_HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME",
        // Windows essentials
        "SystemRoot", "windir", "ComSpec", "PATHEXT", "SystemDrive",
        "USERPROFILE", "APPDATA", "LOCALAPPDATA", "ProgramData",
        "ProgramFiles", "ProgramFiles(x86)", "CommonProgramFiles",
        "NUMBER_OF_PROCESSORS", "PROCESSOR_ARCHITECTURE", "OS",
    ];

    public static Dictionary<string, string> Build(
        IDictionary source,
        IEnumerable<string> additionalAllowedNames,
        IDictionary<string, string> alwaysSet)
    {
        var allowed = new HashSet<string>(DefaultAllowList, StringComparer.OrdinalIgnoreCase);

        foreach (var name in additionalAllowedNames)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                allowed.Add(name.Trim());
            }
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in source)
        {
            var key = entry.Key.ToString();
            if (string.IsNullOrEmpty(key)) continue;

            if (allowed.Contains(key) || key.StartsWith("LC_", StringComparison.OrdinalIgnoreCase))
            {
                result[key] = entry.Value?.ToString() ?? string.Empty;
            }
        }

        foreach (var kvp in alwaysSet)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }
}
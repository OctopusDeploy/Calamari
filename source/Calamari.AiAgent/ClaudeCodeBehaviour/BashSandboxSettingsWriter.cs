using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.ClaudeCodeBehaviour
{
    // Builds Claude Code's built-in bash sandbox settings.json for Bash sandbox mode. The embedded
    // DefaultContext/bash-sandbox-settings.json is the hardened baseline (fail-closed, escape hatch off,
    // credential dirs denied); the user's multiline allow/deny lists are appended (defaults are always
    // retained — merge, not replace), mirroring SrtSettingsWriter.
    //
    // Serialization uses a low-level Utf8JsonWriter for the same reason as SrtSettingsWriter: the
    // published (trimmed) Calamari runtime serializes JSON without a reflection-based TypeInfoResolver,
    // and writing primitives directly needs no resolver.
    public static class BashSandboxSettingsWriter
    {
        const string ClaudeDirName = ".claude";
        const string SettingsFileName = "settings.json";
        const string ResourceName = "Calamari.AiAgent.ClaudeCodeBehaviour.DefaultContext.bash-sandbox-settings.json";

        // Writes the merged bash sandbox settings to <workingDir>/.claude/settings.json and returns the path.
        public static string Write(string workingDir, IVariables variables)
        {
            var claudeDir = Directory.CreateDirectory(Path.Combine(workingDir, ClaudeDirName));
            var destPath = Path.Combine(claudeDir.FullName, SettingsFileName);
            File.WriteAllText(destPath, BuildMergedSettings(LoadBaseline(), variables));
            return destPath;
        }

        internal static string LoadBaseline()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                               ?? throw new Exception($"Could not find embedded {SettingsFileName} (bash sandbox) resource.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // Merges the user's multiline lists onto the baseline JSON and serializes the result.
        internal static string BuildMergedSettings(string baselineJson, IVariables variables)
        {
            using var baseline = JsonDocument.Parse(baselineJson);
            var sandbox = Section(baseline.RootElement, "sandbox");
            var network = Section(sandbox, "network");
            var filesystem = Section(sandbox, "filesystem");

            var enabled = BaselineBool(sandbox, "enabled", true);
            var failIfUnavailable = BaselineBool(sandbox, "failIfUnavailable", true);
            var allowUnsandboxedCommands = BaselineBool(sandbox, "allowUnsandboxedCommands", false);

            var allowedDomains = Merge(BaselineValues(network, "allowedDomains"), variables.Get(SpecialVariables.Action.Claude.BashNetworkAllowedDomains));
            var deniedDomains = Merge(BaselineValues(network, "deniedDomains"), variables.Get(SpecialVariables.Action.Claude.BashNetworkDeniedDomains));
            var allowWrite = Merge(BaselineValues(filesystem, "allowWrite"), variables.Get(SpecialVariables.Action.Claude.BashFilesystemAllowWrite));
            var denyWrite = Merge(BaselineValues(filesystem, "denyWrite"), variables.Get(SpecialVariables.Action.Claude.BashFilesystemDenyWrite));
            var denyRead = Merge(BaselineValues(filesystem, "denyRead"), variables.Get(SpecialVariables.Action.Claude.BashFilesystemDenyRead));
            var allowRead = Merge(BaselineValues(filesystem, "allowRead"), variables.Get(SpecialVariables.Action.Claude.BashFilesystemAllowRead));
            var excludedCommands = Merge(BaselineValues(sandbox, "excludedCommands"), variables.Get(SpecialVariables.Action.Claude.BashExcludedCommands));

            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteStartObject("sandbox");

                writer.WriteBoolean("enabled", enabled);
                writer.WriteBoolean("failIfUnavailable", failIfUnavailable);
                writer.WriteBoolean("allowUnsandboxedCommands", allowUnsandboxedCommands);

                writer.WriteStartObject("network");
                WriteStringArray(writer, "allowedDomains", allowedDomains);
                WriteStringArray(writer, "deniedDomains", deniedDomains);
                writer.WriteEndObject();

                writer.WriteStartObject("filesystem");
                WriteStringArray(writer, "allowWrite", allowWrite);
                WriteStringArray(writer, "denyWrite", denyWrite);
                WriteStringArray(writer, "denyRead", denyRead);
                WriteStringArray(writer, "allowRead", allowRead);
                writer.WriteEndObject();

                WriteStringArray(writer, "excludedCommands", excludedCommands);

                writer.WriteEndObject(); // sandbox
                writer.WriteEndObject(); // root
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        static void WriteStringArray(Utf8JsonWriter writer, string propertyName, List<string> values)
        {
            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteStringValue(value);
            }

            writer.WriteEndArray();
        }

        static JsonElement Section(JsonElement parent, string name)
            => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var section)
                ? section
                : default;

        static bool BaselineBool(JsonElement section, string key, bool fallback)
            => section.ValueKind == JsonValueKind.Object && section.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : fallback;

        static List<string> BaselineValues(JsonElement section, string key)
        {
            var result = new List<string>();
            if (section.ValueKind == JsonValueKind.Object && section.TryGetProperty(key, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            result.Add(value);
                        }
                    }
                }
            }

            return result;
        }

        // Appends the user's entries to the baseline (default) list, retaining defaults and de-duping.
        static List<string> Merge(List<string> baseline, string? userValue)
        {
            var merged = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void Add(string entry)
            {
                if (seen.Add(entry))
                {
                    merged.Add(entry);
                }
            }

            foreach (var entry in baseline)
            {
                Add(entry);
            }

            foreach (var entry in ParseList(userValue))
            {
                Add(entry);
            }

            return merged;
        }

        // Splits a multiline value into trimmed, non-empty entries.
        internal static IEnumerable<string> ParseList(string? value)
            => string.IsNullOrWhiteSpace(value)
                ? []
                : value.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

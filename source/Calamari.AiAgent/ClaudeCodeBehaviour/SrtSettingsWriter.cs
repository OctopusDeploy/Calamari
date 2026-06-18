using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.ClaudeCodeBehaviour
{
    // Builds the srt settings file for Srt sandbox mode. The embedded DefaultContext/srt-settings.json
    // is the baseline; the user's multiline lists are appended (defaults are always retained — merge,
    // not replace).
    //
    // Serialization uses a low-level Utf8JsonWriter rather than JsonNode.ToJsonString / JsonSerializer.
    // The published (trimmed) Calamari runtime serializes JSON without a reflection-based
    // TypeInfoResolver, and a JsonNode holding CLR-created string values cannot be serialized under that
    // constraint (it throws "JsonSerializerOptions instance must specify a TypeInfoResolver setting").
    // Writing primitives directly needs no resolver and works in every runtime configuration.
    public static class SrtSettingsWriter
    {
        const string SrtSettingsFileName = "srt-settings.json";
        const string ResourceName = "Calamari.AiAgent.ClaudeCodeBehaviour.DefaultContext.srt-settings.json";

        // Writes the merged srt settings to <workingDir>/srt-settings.json and returns the path.
        public static string Write(string workingDir, IVariables variables)
        {
            var merged = BuildMergedSettings(LoadBaseline(), variables);
            var destPath = Path.Combine(workingDir, SrtSettingsFileName);
            File.WriteAllText(destPath, merged);
            return destPath;
        }

        internal static string LoadBaseline()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                               ?? throw new Exception($"Could not find embedded {SrtSettingsFileName} resource.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // Merges the user's multiline lists onto the baseline JSON and serializes the result.
        internal static string BuildMergedSettings(string baselineJson, IVariables variables)
        {
            using var baseline = JsonDocument.Parse(baselineJson);
            var root = baseline.RootElement;
            var network = Section(root, "network");
            var filesystem = Section(root, "filesystem");

            var allowedDomains = Merge(BaselineValues(network, "allowedDomains"), variables.Get(SpecialVariables.Action.Claude.SrtNetworkAllowedDomains));
            var deniedDomains = Merge(BaselineValues(network, "deniedDomains"), variables.Get(SpecialVariables.Action.Claude.SrtNetworkDeniedDomains));
            var allowWrite = Merge(BaselineValues(filesystem, "allowWrite"), variables.Get(SpecialVariables.Action.Claude.SrtFilesystemAllowWrite));
            var denyWrite = Merge(BaselineValues(filesystem, "denyWrite"), variables.Get(SpecialVariables.Action.Claude.SrtFilesystemDenyWrite));
            var denyRead = Merge(BaselineValues(filesystem, "denyRead"), variables.Get(SpecialVariables.Action.Claude.SrtFilesystemDenyRead));
            var allowRead = Merge(BaselineValues(filesystem, "allowRead"), variables.Get(SpecialVariables.Action.Claude.SrtFilesystemAllowRead));

            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

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

                writer.WriteEndObject();
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

        static JsonElement Section(JsonElement root, string name)
            => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var section)
                ? section
                : default;

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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Calamari.Testing.Helpers;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    public class ToolManifest
    {
        readonly Dictionary<string, ToolDefinition> tools;

        ToolManifest(Dictionary<string, ToolDefinition> tools)
        {
            this.tools = tools;
        }

        public IReadOnlyCollection<string> ToolNames => tools.Keys.ToList();

        public ToolDefinition? GetTool(string name)
        {
            return tools.TryGetValue(name, out var tool) ? tool : null;
        }

        public static ToolManifest Load()
        {
            var manifestPath = Path.Combine(TestEnvironment.CurrentWorkingDirectory, "tool-manifest.json");
            var json = File.ReadAllText(manifestPath);
            var doc = JsonSerializer.Deserialize<ManifestDocument>(json)
                      ?? throw new InvalidOperationException("Failed to deserialize tool-manifest.json");

            var tools = new Dictionary<string, ToolDefinition>();
            foreach (var (name, entry) in doc.Tools)
            {
                tools[name] = new ToolDefinition(
                    name,
                    ParseVersion(entry.Lowest),
                    ParseVersion(entry.Highest),
                    entry.Source,
                    entry.Architectures);
            }

            return new ToolManifest(tools);
        }

        static Version ParseVersion(string version)
        {
            // Strip leading 'v' if present (e.g., "v0.7.10" -> "0.7.10")
            var clean = version.TrimStart('v');
            return Version.Parse(clean);
        }

        class ManifestDocument
        {
            [JsonPropertyName("tools")]
            public Dictionary<string, ManifestEntry> Tools { get; set; } = new();
        }

        class ManifestEntry
        {
            [JsonPropertyName("lowest")]
            public string Lowest { get; set; } = "";

            [JsonPropertyName("highest")]
            public string Highest { get; set; } = "";

            [JsonPropertyName("source")]
            public string Source { get; set; } = "";

            [JsonPropertyName("architectures")]
            public string[] Architectures { get; set; } = Array.Empty<string>();
        }
    }

    public class ToolDefinition
    {
        public ToolDefinition(string name, Version lowest, Version highest, string source, string[] architectures)
        {
            Name = name;
            Lowest = lowest;
            Highest = highest;
            Source = source;
            Architectures = architectures;
        }

        public string Name { get; }
        public Version Lowest { get; }
        public Version Highest { get; }
        public string Source { get; }
        public string[] Architectures { get; }

        public bool IsInRange(Version version)
        {
            return version >= Lowest && version <= Highest;
        }
    }
}

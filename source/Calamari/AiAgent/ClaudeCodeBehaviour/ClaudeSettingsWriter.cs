using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class ClaudeSettingsWriter(ICalamariFileSystem fileSystem, ILog log)
{
    readonly List<IClaudeSettingsJson> sources = new();

    public ClaudeSettingsWriter Add(IClaudeSettingsJson source)
    {
        sources.Add(source);
        return this;
    }

    public bool HasSettings => sources.Count > 0;

    // Merges every added settings source into a single document and writes it to filePath.
    // Claude's --settings flag only accepts one file :(
    public string Write(string filePath)
    {
        var merged = new JsonObject();
        foreach (var source in sources)
            Merge(merged, source.Build(), "");

        fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(filePath)!);
        fileSystem.WriteAllText(filePath, merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return filePath;
    }

    // Please remove this from existence once runtime adds a JSON merge
    // ref: https://github.com/dotnet/runtime/issues/31433
    void Merge(JsonObject target, JsonObject source, string path)
    {
        foreach (var property in source)
        {
            var keyPath = path.Length == 0 ? property.Key : $"{path}.{property.Key}";

            if (target[property.Key] is JsonObject targetObject && property.Value is JsonObject sourceObject)
                Merge(targetObject, sourceObject, keyPath);
            else if (target[property.Key] is JsonArray targetArray && property.Value is JsonArray sourceArray)
                foreach (var item in sourceArray)
                    targetArray.Add(item?.DeepClone());
            else
            {
                if (target.ContainsKey(property.Key))
                    log.Warn($"Claude settings merge is overwriting '{keyPath}'; the last settings source wins.");

                target[property.Key] = property.Value?.DeepClone();
            }
        }
    }
}

using System;
using System.IO;
using System.Text.Json;

public class RuntimeTargetParser
{
    public class RuntimeTarget
    {
        public string Framework { get; set; }
        public string Runtime { get; set; }
    }

    public static RuntimeTarget ParseFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        using var document = JsonDocument.Parse(json);

        var runtimeTargetName = document.RootElement
            .GetProperty("runtimeTarget")
            .GetProperty("name")
            .GetString();

        return ParseRuntimeTarget(runtimeTargetName);
    }

    public static RuntimeTarget ParseRuntimeTarget(string runtimeTargetName)
    {
        var parts = runtimeTargetName.Split('/');

        return new RuntimeTarget
        {
            Framework = parts[0],
            Runtime = parts.Length > 1 ? parts[1] : null
        };
    }
}
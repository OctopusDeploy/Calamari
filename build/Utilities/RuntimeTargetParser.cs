using System;
using System.IO;
using System.Text.Json;

namespace Calamari.Build.Utilities;

public class RuntimeTargetParser
{
    public class RuntimeTarget
    {
        public RuntimeTarget(string runtimeTargetName)
        {
            var parts = runtimeTargetName.Split('/');
        
            if (parts.Length == 0 || parts.Length > 2)
                throw new InvalidOperationException($"Could not parse the runtime target name '{runtimeTargetName}'.");

            Framework = parts[0].Replace(".NETCoreApp,Version=v", "net");
            if (parts.Length == 2)
                Runtime = parts[1];
        }

        public string Framework { get; init; }
        public string? Runtime { get; init; }
    }

    public static RuntimeTarget ParseFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        using var document = JsonDocument.Parse(json);

        var runtimeTargetName = document.RootElement
                                        .GetProperty("runtimeTarget")
                                        .GetProperty("name")
                                        .GetString();

        if (string.IsNullOrEmpty(runtimeTargetName))
            throw new InvalidOperationException("Could not find a valid runtime target name in the provided file.");
        
        return new RuntimeTarget(runtimeTargetName);
    }
}

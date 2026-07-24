using System;
using System.IO;
using System.Reflection;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class SystemPromptWriter
{
    public string WriteSystemPromptFile(string workingDir)
    {
        var res = $"{GetType().Namespace}.DefaultContext.system-prompt.md";
        var assembly = Assembly.GetExecutingAssembly();
        var path = Path.Combine(workingDir, "system-prompt.md");

        using var stream = assembly.GetManifestResourceStream(res);
        if (stream == null)
        {
            throw new Exception($"Could not find expected system prompt embedded resource.");
        }

        using var reader = new StreamReader(stream);
        File.WriteAllText(path, reader.ReadToEnd());

        return path;
    }
}
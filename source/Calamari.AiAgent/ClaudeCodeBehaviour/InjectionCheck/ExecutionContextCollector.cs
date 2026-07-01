using System;
using System.IO;
using System.Text;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AiAgent.ClaudeCodeBehaviour.InjectionCheck;

// Gathers the on-disk execution context (plus the in-memory prompt) into a single payload that is
// handed to the classifier as untrusted data. Everything here is read after it has been written by
// McpWriter/SkillsWriter/SystemPromptWriter and the deployment-variables dump.
public class ExecutionContextCollector(int maxInputCharacters, ILog log)
{
    readonly string truncationText = Environment.NewLine + "[truncated]";

    public string Collect(string workingDir, string prompt)
    {
        
        var sb = new StringBuilder();

        AppendSection(sb, "USER PROMPT", prompt);
        AppendFileSection(sb, "SYSTEM PROMPT (system-prompt.md)", Path.Combine(workingDir, "system-prompt.md"));
        AppendFileSection(sb, "DEPLOYMENT VARIABLES (deployment-variables.json)", Path.Combine(workingDir, "deployment-variables.json"));
        AppendFileSection(sb, "MCP SERVERS (mcp-config.json)", Path.Combine(workingDir, "mcp-config.json"));
        AppendSkills(sb, workingDir);

        var text = sb.ToString();
        if (text.Length <= (maxInputCharacters - truncationText.Length))
            return text;
        
        log.Warn($"Prompt is too long ({text.Length} characters, max {maxInputCharacters}). Truncating.");

        text = text[..maxInputCharacters] + truncationText;

        return text;
    }

    static void AppendSkills(StringBuilder sb, string workingDir)
    {
        var skillsDir = Path.Combine(workingDir, ".claude", "skills");
        if (!Directory.Exists(skillsDir))
            return;

        //todo: validate SKILLS.md compared to everything - for example, if a skill has extra context that it requests to be injected
        foreach (var skillFile in Directory.EnumerateFiles(skillsDir, "SKILL.md", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(Path.GetDirectoryName(skillFile));
            AppendFileSection(sb, $"SKILL: {name}", skillFile);
        }
    }

    static void AppendFileSection(StringBuilder sb, string label, string path)
    {
        if (!File.Exists(path))
            return;

        AppendSection(sb, label, File.ReadAllText(path));
    }

    static void AppendSection(StringBuilder sb, string label, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        sb.Append("===== ").Append(label).Append(" =====").Append(Environment.NewLine);
        sb.Append(content).Append(Environment.NewLine).Append(Environment.NewLine);
    }
}

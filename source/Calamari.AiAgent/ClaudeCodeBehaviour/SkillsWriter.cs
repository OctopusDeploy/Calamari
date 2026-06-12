using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

public class SkillsWriter(IVariables variables)
{
    const string SkillsResourcePrefix = "Calamari.AiAgent.ClaudeCodeBehaviour.DefaultContext.Skills.";

    public void SetupSkills(string workingDir)
    {
        var skillsDir = Path.Combine(workingDir, ".claude", "skills");
        Directory.CreateDirectory(skillsDir);

        CreateSysemSkillFiles(skillsDir);
        CreateUserSkillFiles(skillsDir);
    }

    static void CreateSysemSkillFiles(string skillsDir)
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(SkillsResourcePrefix, StringComparison.Ordinal))
                continue;

            var fileName = resourceName.Substring(SkillsResourcePrefix.Length);
            var skillName = Path.GetFileNameWithoutExtension(fileName);
            var innerSkillDir = Path.Combine(skillsDir, skillName);

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);

            Directory.CreateDirectory(innerSkillDir);
            File.WriteAllText(Path.Combine(innerSkillDir, "SKILL.md"), reader.ReadToEnd());
        }
    }

    void CreateUserSkillFiles(string skillsDir)
    {
        var userSkills = BuildUserSkills();

        foreach (var skill in userSkills)
        {
            var dirName = SanitizeFileName(skill.Name);
            var innerSkillDir = Path.GetFullPath(Path.Combine(skillsDir, dirName));

            if (!innerSkillDir.StartsWith(Path.GetFullPath(skillsDir) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new CommandException($"Skill name '{skill.Name}' results in a path outside the skills directory.");

            Directory.CreateDirectory(innerSkillDir);
            File.WriteAllText(Path.Combine(innerSkillDir, "SKILL.md"), skill.Content);
        }
    }

    List<UserSkill> BuildUserSkills()
    {
        var skills = new List<UserSkill>();
        var indexes = variables.GetIndexes(SpecialVariables.Action.AiAgent.Skills);
        foreach (var index in indexes)
        {
            var prefix = $"{SpecialVariables.Action.AiAgent.Skills}[{index}].";
            var name = variables.Get(prefix + SpecialVariables.Action.AiAgent.SkillName);
            var content = variables.Get(prefix + SpecialVariables.Action.AiAgent.SkillContent);

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(content))
                skills.Add(new UserSkill { Name = name, Content = content });
        }
        return skills;
    }

    static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    internal static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new CommandException("Skill name cannot be empty.");

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (Array.IndexOf(invalid, c) >= 0 || char.IsControl(c) || c is '<' or '>' or ':' or '"' or '|' or '?' or '*' or '\\')
                sanitized.Append('-');
            else
                sanitized.Append(c);
        }

        // Strip leading dots to prevent hidden files / relative path tricks
        var result = sanitized.ToString().TrimStart('.');

        if (string.IsNullOrWhiteSpace(result))
            throw new CommandException($"Skill name '{name}' is not a valid file name.");

        if (WindowsReservedNames.Contains(result))
            throw new CommandException($"Skill name '{name}' is a reserved file name.");

        // Filesystem limits are typically 255 bytes; truncate to be safe
        if (result.Length > 200)
            result = result.Substring(0, 200);

        return result;
    }
}
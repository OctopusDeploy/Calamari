using System.IO;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class SkillsWriterFixture
{
    string workingDir = null!;

    [SetUp]
    public void SetUp()
    {
        workingDir = Path.Combine(Path.GetTempPath(), $"test-skills-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(workingDir))
            Directory.Delete(workingDir, true);
    }

    static CalamariVariables EmptyVariables() => new();

    static CalamariVariables VariablesWithSkills(params (string Name, string Content)[] skills)
    {
        var vars = new CalamariVariables();
        for (var i = 0; i < skills.Length; i++)
        {
            vars.Set($"{SpecialVariables.Action.Claude.Skills}[{i}].{SpecialVariables.Action.Claude.SkillName}", skills[i].Name);
            vars.Set($"{SpecialVariables.Action.Claude.Skills}[{i}].{SpecialVariables.Action.Claude.SkillContent}", skills[i].Content);
        }
        return vars;
    }

    [Test]
    public void SetupSkills_CreatesSkillDirectories()
    {
        new SkillsWriter(EmptyVariables()).SetupSkills(workingDir);

        var skillDir = Path.Combine(workingDir, ".claude", "skills", "octopus-deployment-context");
        Directory.Exists(skillDir).Should().BeTrue();

        var skillMd = Path.Combine(skillDir, "SKILL.md");
        File.Exists(skillMd).Should().BeTrue();

        var content = File.ReadAllText(skillMd);
        content.Should().Contain("get_deployment_variables");
    }

    [Test]
    public void SetupSkills_WritesArtifactsSkill()
    {
        new SkillsWriter(EmptyVariables()).SetupSkills(workingDir);

        var skillMd = Path.Combine(workingDir, ".claude", "skills", "octopus-artifacts", "SKILL.md");
        File.Exists(skillMd).Should().BeTrue();

        var content = File.ReadAllText(skillMd);
        content.Should().Contain(".octopus/artifacts.jsonl");
    }

    [Test]
    public void SetupSkills_WritesUserSkills()
    {
        var vars = VariablesWithSkills(
            ("my-custom-skill", "---\nname: my-custom-skill\n---\nDo something useful."),
            ("another-skill", "---\nname: another-skill\n---\nMore instructions."));

        new SkillsWriter(vars).SetupSkills(workingDir);

        var skill1 = Path.Combine(workingDir, ".claude", "skills", "my-custom-skill", "SKILL.md");
        File.Exists(skill1).Should().BeTrue();
        File.ReadAllText(skill1).Should().Contain("Do something useful.");

        var skill2 = Path.Combine(workingDir, ".claude", "skills", "another-skill", "SKILL.md");
        File.Exists(skill2).Should().BeTrue();
        File.ReadAllText(skill2).Should().Contain("More instructions.");
    }

    [Test]
    public void SetupSkills_SanitizesPathTraversalAttempt()
    {
        var vars = VariablesWithSkills(("../../etc/evil", "content"));

        new SkillsWriter(vars).SetupSkills(workingDir);

        var skillsDir = Path.Combine(workingDir, ".claude", "skills");
        var dirs = Directory.GetDirectories(skillsDir);
        dirs.Should().Contain(d => Path.GetFileName(d).Contains("etc-evil"));

        File.Exists(Path.Combine(workingDir, "..", "..", "etc", "evil", "SKILL.md")).Should().BeFalse();
    }

    [Test]
    public void SetupSkills_SkipsSkillsWithEmptyNameOrContent()
    {
        var vars = new CalamariVariables();
        vars.Set($"{SpecialVariables.Action.Claude.Skills}[0].{SpecialVariables.Action.Claude.SkillName}", "");
        vars.Set($"{SpecialVariables.Action.Claude.Skills}[0].{SpecialVariables.Action.Claude.SkillContent}", "some content");
        vars.Set($"{SpecialVariables.Action.Claude.Skills}[1].{SpecialVariables.Action.Claude.SkillName}", "valid-name");
        vars.Set($"{SpecialVariables.Action.Claude.Skills}[1].{SpecialVariables.Action.Claude.SkillContent}", "");

        new SkillsWriter(vars).SetupSkills(workingDir);

        var skillsDir = Path.Combine(workingDir, ".claude", "skills");
        Directory.Exists(Path.Combine(skillsDir, "valid-name")).Should().BeFalse();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void SanitizeFileName_RejectsEmptyOrWhitespace(string name)
    {
        var act = () => SkillsWriter.SanitizeFileName(name!);
        act.Should().Throw<CommandException>().WithMessage("*cannot be empty*");
    }

    [TestCase("CON")]
    [TestCase("con")]
    [TestCase("NUL")]
    [TestCase("COM1")]
    [TestCase("LPT3")]
    public void SanitizeFileName_RejectsWindowsReservedNames(string name)
    {
        var act = () => SkillsWriter.SanitizeFileName(name);
        act.Should().Throw<CommandException>().WithMessage("*reserved*");
    }

    [Test]
    public void SanitizeFileName_StripsLeadingDots()
    {
        SkillsWriter.SanitizeFileName("...my-skill").Should().Be("my-skill");
    }

    [Test]
    public void SanitizeFileName_ReplacesPathSeparators()
    {
        var result = SkillsWriter.SanitizeFileName("../../etc/passwd");
        result.Should().NotContain("/");
        result.Should().NotContain("\\");
    }

    [Test]
    public void SanitizeFileName_ReplacesControlCharacters()
    {
        var result = SkillsWriter.SanitizeFileName("my\tskill\nname");
        result.Should().NotContainAny("\t", "\n");
        result.Should().Contain("my");
        result.Should().Contain("skill");
        result.Should().Contain("name");
    }

    [Test]
    public void SanitizeFileName_ReplacesWindowsUnsafeCharsOnAllPlatforms()
    {
        var result = SkillsWriter.SanitizeFileName("skill<name>with|pipes");
        result.Should().NotContainAny("<", ">", "|");
    }

    [Test]
    public void SanitizeFileName_TruncatesLongNames()
    {
        var longName = new string('a', 300);
        SkillsWriter.SanitizeFileName(longName).Length.Should().BeLessOrEqualTo(200);
    }
}

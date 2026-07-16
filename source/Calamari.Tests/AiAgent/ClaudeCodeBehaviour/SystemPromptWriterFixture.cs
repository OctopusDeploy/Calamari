using System.IO;
using Calamari.AiAgent.ClaudeCodeBehaviour;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class SystemPromptWriterFixture
{
    string workingDir = null!;

    [SetUp]
    public void SetUp()
    {
        workingDir = Path.Combine(Path.GetTempPath(), $"test-sysprompt-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(workingDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(workingDir))
            Directory.Delete(workingDir, true);
    }

    [Test]
    public void WriteSystemPromptFile_WritesFile()
    {
        var path = new SystemPromptWriter().WriteSystemPromptFile(workingDir);

        File.Exists(path).Should().BeTrue();
        File.ReadAllText(path).Should().NotBeEmpty();
    }
}

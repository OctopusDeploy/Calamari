using Calamari.AiAgent.ClaudeCodeBehaviour;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests.ClaudeCodeBehaviour;

[TestFixture]
public class SrtVersionGuardFixture
{
    [TestCase("0.0.55", true)]
    [TestCase("0.0.56", true)]
    [TestCase("0.1.0", true)]
    [TestCase("1.0.0", true)]
    [TestCase("0.0.54", false)]
    [TestCase("0.0.1", false)]
    [TestCase("srt version 0.0.60 (build abc)", true)]
    [TestCase("@anthropic-ai/sandbox-runtime 0.0.55", true)]
    public void MeetsMinimum_ParsesAndComparesSemver(string output, bool expected)
    {
        SrtVersionGuard.MeetsMinimum(output, out _).Should().Be(expected);
    }

    [TestCase("")]
    [TestCase("not a version")]
    [TestCase(null)]
    public void MeetsMinimum_ReturnsFalse_WhenNoVersionFound(string output)
    {
        SrtVersionGuard.MeetsMinimum(output, out var parsed).Should().BeFalse();
        parsed.Should().BeNull();
    }
}
